using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Domain.Clipboard;
using Windows.ApplicationModel.DataTransfer;

namespace NovaIsland.Infrastructure.Clipboard;

public class ClipboardListenerService : IClipboardService, IDisposable
{
    private readonly IDbContextFactory<ClipboardDbContext> _dbContextFactory;
    private readonly IOptionsMonitor<ClipboardOptions> _options;
    private readonly ILogger<ClipboardListenerService> _logger;
    private bool _isDisposed;

    public event EventHandler<ClipboardEntry>? EntryAdded;

    public ClipboardListenerService(
        IDbContextFactory<ClipboardDbContext> dbContextFactory,
        IOptionsMonitor<ClipboardOptions> options,
        ILogger<ClipboardListenerService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    public void StartListening()
    {
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnClipboardContentChanged;
    }

    private void OnClipboardContentChanged(object? sender, object e)
    {
        // Offload to background thread to ensure <10ms overhead on the event handler.
        _ = Task.Run(ProcessClipboardContentAsync);
    }

    private async Task ProcessClipboardContentAsync()
    {
        try
        {
            var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            var entry = new ClipboardEntry
            {
                Timestamp = DateTimeOffset.UtcNow
            };

            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                var text = await dataPackageView.GetTextAsync();
                entry.Type = ClipboardEntryType.Text;
                entry.Content = text;
                entry.IsSensitive = CheckSensitivity(text);
            }
            else if (dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await dataPackageView.GetStorageItemsAsync();
                entry.Type = ClipboardEntryType.File;
                entry.Content = string.Join(Environment.NewLine, items.Select(i => i.Path));
            }
            else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                entry.Type = ClipboardEntryType.Image;
                // Skipping bitmap blob extraction for brevity in this phase, 
                // but this is where RandomAccessStream would be read into a byte[]
            }
            else
            {
                return; // Unsupported format
            }

            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Entries.Add(entry);
            await context.SaveChangesAsync();

            EntryAdded?.Invoke(this, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture clipboard content");
        }
    }

    private bool CheckSensitivity(string content)
    {
        var patterns = _options.CurrentValue.SensitivePatterns;
        if (patterns == null || patterns.Count == 0) return false;

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<IEnumerable<ClipboardEntry>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Entries
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ClipboardEntry>> SearchAsync(string query, int limit = 50, CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Entries
            .Where(e => e.Content != null && e.Content.Contains(query))
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task PinEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await context.Entries.FindAsync(new object[] { id }, cancellationToken);
        if (entry != null)
        {
            entry.IsPinned = true;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnpinEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await context.Entries.FindAsync(new object[] { id }, cancellationToken);
        if (entry != null)
        {
            entry.IsPinned = false;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await context.Entries.FindAsync(new object[] { id }, cancellationToken);
        if (entry != null)
        {
            context.Entries.Remove(entry);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed) return;
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= OnClipboardContentChanged;
        _isDisposed = true;
    }
}
