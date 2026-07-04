using NovaIsland.Domain.Widgets;

namespace NovaIsland.Application.Widgets.FirstParty;

[WidgetManifest("Battery", "Battery Widget", WidgetCapabilities.None)]
public class BatteryWidget : IWidget
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(2, cancellationToken);
    }

    public Task RenderAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GetSummaryText() => "Battery: 85%";

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
