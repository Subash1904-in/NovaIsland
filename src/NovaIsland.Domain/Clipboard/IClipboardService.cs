namespace NovaIsland.Domain.Clipboard;

public interface IClipboardService
{
    Task<IEnumerable<ClipboardEntry>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);
    
    void StartListening();
    
    Task<IEnumerable<ClipboardEntry>> SearchAsync(string query, int limit = 50, CancellationToken cancellationToken = default);
    Task PinEntryAsync(Guid id, CancellationToken cancellationToken = default);
    Task UnpinEntryAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default);
    
    event EventHandler<ClipboardEntry>? EntryAdded;
}
