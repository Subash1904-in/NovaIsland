using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Clipboard;

namespace NovaIsland.Application.Modules;

public class ClipboardModule : INovaModule
{
    private readonly IClipboardService _clipboardService;
    // We are expecting dynamic loading here via IOptionsMonitor.
    private readonly IOptionsMonitor<ClipboardOptions> _options;
    private readonly ILogger<ClipboardModule> _logger;

    public string ModuleName => "Clipboard";

    public ClipboardModule(
        IClipboardService clipboardService,
        IOptionsMonitor<ClipboardOptions> options, // using IOptionsMonitor for dynamic reload
        ILogger<ClipboardModule> logger)
    {
        _clipboardService = clipboardService;
        _options = options;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clipboard Module started");

        // Start listening to the clipboard
        if (_clipboardService.GetType().GetMethod("StartListening") != null)
        {
            _clipboardService.GetType().GetMethod("StartListening")!.Invoke(_clipboardService, null);
        }

        _clipboardService.EntryAdded += OnEntryAdded;

        try
        {
            // Retention check loop
            while (!cancellationToken.IsCancellationRequested)
            {
                await PerformRetentionCleanupAsync(cancellationToken);
                
                // Configurable cleanup interval, maybe every hour or so, but we'll do every 5 mins for now.
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Normal stop
        }
        finally
        {
            _clipboardService.EntryAdded -= OnEntryAdded;
            _logger.LogInformation("Clipboard Module stopped");
        }
    }

    private void OnEntryAdded(object? sender, ClipboardEntry e)
    {
        _logger.LogInformation("Clipboard entry added: {Id}, Type: {Type}, Sensitive: {Sensitive}", e.Id, e.Type, e.IsSensitive);
        // Could surface to UI if configured, but for now we just persist it.
    }

    private async Task PerformRetentionCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retentionPeriod = _options.CurrentValue.RetentionPeriod;
            var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
            
            // Get unpinned entries older than the cutoff.
            // (In a real scenario, we'd add a method to IClipboardService to do this efficiently in the DB).
            var history = await _clipboardService.GetHistoryAsync(limit: 1000, cancellationToken: cancellationToken);
            var toEvict = history.Where(e => !e.IsPinned && e.Timestamp < cutoff);

            foreach (var entry in toEvict)
            {
                await _clipboardService.DeleteEntryAsync(entry.Id, cancellationToken);
            }
            
            _logger.LogInformation("Evicted older clipboard entries based on retention policy.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing clipboard retention cleanup");
        }
    }
}
