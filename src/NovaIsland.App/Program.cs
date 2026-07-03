using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovaIsland.Infrastructure;
using NovaIsland.Infrastructure.Logging;
using NovaIsland.UI;
using NovaIsland.Application.Modules;
using NovaIsland.Application.Widgets.FirstParty;
using NovaIsland.Domain.Widgets;
using Serilog;
using Velopack;

namespace NovaIsland.App;

/// <summary>
/// Nova Island application entry point.
/// Configures the Generic Host with Serilog logging, infrastructure services,
/// stability subsystem, and all module registrations. Designed to be Native AOT compatible.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. Bootstraps the Generic Host with full
    /// DI container, Serilog pipeline, stability subsystem, and module services.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 = clean shutdown).</returns>
    public static async Task<int> Main(string[] args)
    {
        // Bootstrap Serilog early so we capture startup errors.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

        try
        {
            // Initialize Velopack for install/update handling
            VelopackApp.Build().Run();

            Log.Information("Nova Island starting — Phase 2 (Stability Core)");

            var builder = Host.CreateApplicationBuilder(args);

            // Register infrastructure services (Serilog, future: data, OS interop).
            builder.AddInfrastructure();

            // Register stability subsystem (heartbeat client, memory guard, crash-loop detection).
            builder.AddStability();

            // Register island shell services (Win32 window, Composition, animation, frame pacing).
            builder.AddIslandShell();

            // Future phases will register supervised modules like:
            builder.Services.AddSupervisedModule<MediaModule>();
            builder.Services.AddSupervisedModule<ClipboardModule>();
            builder.Services.AddSupervisedModule<NotificationModule>();
            builder.Services.AddSupervisedModule<WidgetModule>();
            builder.Services.AddSupervisedModule<AiModule>();
            builder.Services.AddSupervisedModule<AutomationModule>();
            builder.Services.AddSupervisedModule<NovaIsland.Plugins.Host.PluginModule>();

            // Register first-party widgets
            builder.Services.AddSingleton<IWidget, WeatherWidget>();
            builder.Services.AddSingleton<IWidget, CalendarWidget>();
            builder.Services.AddSingleton<IWidget, BatteryWidget>();
            builder.Services.AddSingleton<IWidget, NetworkWidget>();

            var host = builder.Build();

            // Create a correlation scope for the application lifetime.
            var correlationContext = host.Services.GetRequiredService<CorrelationContext>();
            using var scope = correlationContext.BeginScope("app-main");

            Log.Information("Nova Island host built successfully. Starting...");

            await host.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Nova Island terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
