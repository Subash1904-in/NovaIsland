namespace NovaIsland.Domain.Widgets;

public interface IWidget : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RenderAsync(CancellationToken cancellationToken = default);
    string GetSummaryText();
}
