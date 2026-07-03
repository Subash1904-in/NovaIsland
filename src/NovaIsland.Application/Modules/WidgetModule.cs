using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Widgets;

namespace NovaIsland.Application.Modules;

public class WidgetModule : INovaModule
{
    private readonly IEnumerable<IWidget> _widgets;
    private readonly ILogger<WidgetModule> _logger;

    public string ModuleName => "Widgets";

    public WidgetModule(IEnumerable<IWidget> widgets, ILogger<WidgetModule> logger)
    {
        _widgets = widgets;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Widget Module started");

        // Load widgets with a strict <100ms budget per widget
        foreach (var widget in _widgets)
        {
            var manifest = widget.GetType().GetCustomAttributes(typeof(WidgetManifestAttribute), false)
                .FirstOrDefault() as WidgetManifestAttribute;

            var widgetName = manifest?.Name ?? widget.GetType().Name;
            var capabilities = manifest?.RequiredCapabilities ?? WidgetCapabilities.None;

            _logger.LogInformation("Loading Widget: {Name} [Capabilities: {Caps}]", widgetName, capabilities);

            var sw = Stopwatch.StartNew();
            try
            {
                // Enforce 100ms timeout for initialization to ensure the UI loop is never blocked for long
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                await widget.InitializeAsync(cts.Token);
                sw.Stop();

                if (sw.ElapsedMilliseconds > 100)
                {
                    _logger.LogWarning("Widget {Name} exceeded 100ms budget ({Elapsed}ms).", widgetName, sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation("Widget {Name} loaded in {Elapsed}ms.", widgetName, sw.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogError("Widget {Name} failed to load within 100ms limit.", widgetName);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Widget {Name} threw an exception during load.", widgetName);
            }
        }

        try
        {
            // The render loop is normally driven by the UI / IslandShellService
            // This module's job is just to host them and keep them alive
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            _logger.LogInformation("Widget Module stopped");
            foreach (var widget in _widgets)
            {
                widget.Dispose();
            }
        }
    }
}
