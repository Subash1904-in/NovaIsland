using NovaIsland.Domain.Widgets;

namespace NovaIsland.Application.Widgets.FirstParty;

[WidgetManifest("Weather", "Weather Widget", WidgetCapabilities.Network | WidgetCapabilities.Location)]
public class WeatherWidget : IWidget
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Simulate network fetch
        await Task.Delay(10, cancellationToken);
    }

    public Task RenderAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
