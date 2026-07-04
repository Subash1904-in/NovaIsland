using NovaIsland.Domain.Widgets;

namespace NovaIsland.Application.Widgets.FirstParty;

[WidgetManifest("Calendar", "Calendar Widget", WidgetCapabilities.None)]
public class CalendarWidget : IWidget
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Local fast load
        await Task.Delay(5, cancellationToken);
    }

    public Task RenderAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GetSummaryText() => "No upcoming meetings";

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
