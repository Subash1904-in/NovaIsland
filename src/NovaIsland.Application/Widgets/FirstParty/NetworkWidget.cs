using NovaIsland.Domain.Widgets;

namespace NovaIsland.Application.Widgets.FirstParty;

[WidgetManifest("Network", "Network Widget", WidgetCapabilities.Network)]
public class NetworkWidget : IWidget
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(2, cancellationToken);
    }

    public Task RenderAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GetSummaryText() => "Network: Connected";

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
