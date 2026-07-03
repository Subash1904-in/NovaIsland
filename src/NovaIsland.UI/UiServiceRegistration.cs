using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovaIsland.UI.Animation;
using NovaIsland.UI.Shell;

namespace NovaIsland.UI;

/// <summary>
/// Extension methods to register all Nova Island UI shell services with the DI container.
/// </summary>
public static class UiServiceRegistration
{
    /// <summary>
    /// Adds the island shell services to the host builder: animation controller,
    /// display refresh detection, DPI awareness, frame pacing, and the shell hosted service.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddIslandShell(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Bind island configuration from appsettings.json.
        builder.Services.Configure<IslandSettings>(
            builder.Configuration.GetSection("Island"));

        // Register the animation controller based on reduced-motion setting.
        builder.Services.AddSingleton<IIslandAnimator>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IslandSettings>>().Value;

            if (settings.ReducedMotion)
            {
                return new ReducedMotionAnimator(settings.InitialState);
            }

            var springConfig = settings.GetSpringConfig();
            return new IslandAnimationController(settings.InitialState, springConfig);
        });

        // Register the shell hosted service.
        builder.Services.AddHostedService<IslandShellService>();

        return builder;
    }
}
