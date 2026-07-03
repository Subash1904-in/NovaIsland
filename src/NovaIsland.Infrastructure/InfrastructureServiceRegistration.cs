using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovaIsland.Application.Stability;
using NovaIsland.Infrastructure.Logging;
using NovaIsland.Infrastructure.Stability;
using NovaIsland.Infrastructure.Clipboard;
using NovaIsland.Infrastructure.Media;
using NovaIsland.Domain.Clipboard;
using NovaIsland.Domain.Media;
using NovaIsland.Domain.Ai;
using NovaIsland.Infrastructure.Ai;
using NovaIsland.Domain.Automation;
using NovaIsland.Infrastructure.Automation;
using Serilog;

namespace NovaIsland.Infrastructure;

/// <summary>
/// Extension methods to register all infrastructure services with the DI container.
/// </summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Adds infrastructure services (logging, future: data, OS interop) to the host builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Configure Serilog with console + rolling-file sinks and correlation-ID enrichment.
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine("logs", "nova-island-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                shared: true)
            .CreateLogger();

        // Register Serilog as the logging provider via the DI-friendly extension.
        builder.Services.AddSerilog(Log.Logger, dispose: true);

        // Register the correlation-ID context as a singleton for cross-cutting use.
        builder.Services.AddSingleton<CorrelationContext>();

        // Register configuration options
        builder.Services.Configure<ClipboardOptions>(
            builder.Configuration.GetSection(ClipboardOptions.SectionName));

        // Register Phase 3 Services
        builder.Services.AddDbContextFactory<ClipboardDbContext>();
        builder.Services.AddSingleton<IClipboardService, ClipboardListenerService>();
        // We'll also register it as itself so we can call StartListening on startup or module init
        builder.Services.AddSingleton(sp => (ClipboardListenerService)sp.GetRequiredService<IClipboardService>());

        builder.Services.AddSingleton<IMediaService, SmtcMediaService>();
        builder.Services.AddSingleton(sp => (SmtcMediaService)sp.GetRequiredService<IMediaService>());

        // Notifications
        builder.Services.AddSingleton<NovaIsland.Domain.Notifications.IFocusAssistProvider, NovaIsland.Infrastructure.Notifications.FocusAssistStateProvider>();
        builder.Services.AddSingleton<NovaIsland.Domain.Notifications.INotificationService, NovaIsland.Infrastructure.Notifications.WindowsNotificationListenerService>();
        // Also register concrete types if needed for startup initialization
        builder.Services.AddSingleton(sp => (NovaIsland.Infrastructure.Notifications.WindowsNotificationListenerService)sp.GetRequiredService<NovaIsland.Domain.Notifications.INotificationService>());

        // AI
        builder.Services.AddSingleton<IAiProvider, DummyCloudAiProvider>();

        // Automation
        builder.Services.AddSingleton<IRuleStore, FileRuleStore>();

        return builder;
    }

    /// <summary>
    /// Adds stability services (watchdog heartbeat client, module supervision infrastructure,
    /// crash-loop detection, memory/handle leak guard) to the host builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddStability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Bind stability options from configuration.
        builder.Services.Configure<StabilityOptions>(
            builder.Configuration.GetSection(StabilityOptions.SectionName));

        // Module health reporter — singleton, shared across all supervised modules.
        builder.Services.AddSingleton<IModuleHealthReporter, ModuleHealthReporter>();

        // Update rollback — stub until Velopack is wired.
        builder.Services.AddSingleton<StubUpdateRollback>();
        builder.Services.AddSingleton<IUpdateRollback>(sp => sp.GetRequiredService<StubUpdateRollback>());

        // Crash-loop detector — singleton, fed by all supervised modules.
        builder.Services.AddSingleton<CrashLoopDetector>();

        // Heartbeat client — sends PING to the watchdog process.
        builder.Services.AddHostedService<HeartbeatClientService>();

        // Memory/handle leak guard — periodic diagnostics and idle trimming.
        builder.Services.AddHostedService<MemoryLeakGuardService>();

        // Phase 8 Performance services
        builder.Services.AddSingleton<IGraphicsTierDetector, GpuTierDetector>();
        builder.Services.AddHostedService<IdleWorkingSetTrimmer>();

        return builder;
    }

    /// <summary>
    /// Registers a <see cref="INovaModule"/> to be run inside a
    /// <see cref="SupervisedModuleService"/> supervision boundary.
    /// </summary>
    /// <typeparam name="TModule">The module type implementing <see cref="INovaModule"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSupervisedModule<TModule>(this IServiceCollection services)
        where TModule : class, INovaModule
    {
        // Register the module itself as a singleton.
        services.AddSingleton<TModule>();

        // Register a supervised service wrapper that resolves the module from DI.
        services.AddSingleton<IHostedService>(sp =>
        {
            var module = sp.GetRequiredService<TModule>();
            var healthReporter = sp.GetRequiredService<IModuleHealthReporter>();
            var crashLoopDetector = sp.GetRequiredService<CrashLoopDetector>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StabilityOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SupervisedModuleService>>();

            return new SupervisedModuleService(module, healthReporter, crashLoopDetector, options, logger);
        });

        return services;
    }
}

