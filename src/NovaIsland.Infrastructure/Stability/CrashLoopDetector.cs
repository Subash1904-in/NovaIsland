using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Application.Stability;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Tracks crash timestamps per module and triggers a rollback via
/// <see cref="IUpdateRollback"/> when a crash loop is detected
/// (N crashes within T minutes for the same module).
/// </summary>
public sealed class CrashLoopDetector
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _crashHistory = new(StringComparer.Ordinal);
    private readonly IUpdateRollback _rollback;
    private readonly StabilityOptions _options;
    private readonly ILogger<CrashLoopDetector> _logger;
    private readonly object _detectionLock = new();

    /// <summary>
    /// Raised when a crash loop is detected. Exposed for testing.
    /// </summary>
    public event EventHandler<string>? CrashLoopDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashLoopDetector"/> class.
    /// </summary>
    /// <param name="rollback">The rollback implementation to invoke.</param>
    /// <param name="options">Stability configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public CrashLoopDetector(
        IUpdateRollback rollback,
        IOptions<StabilityOptions> options,
        ILogger<CrashLoopDetector> logger)
    {
        _rollback = rollback;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Records a crash for the specified module and checks if the crash-loop
    /// threshold has been exceeded.
    /// </summary>
    /// <param name="moduleName">The module that crashed.</param>
    /// <returns>A task that completes when the crash has been recorded and any rollback triggered.</returns>
    public async Task RecordCrashAsync(string moduleName)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-_options.CrashLoopWindowMinutes);

        var crashes = _crashHistory.GetOrAdd(moduleName, _ => new List<DateTimeOffset>());

        bool shouldRollback;

        lock (_detectionLock)
        {
            crashes.Add(now);

            // Remove crashes outside the detection window.
            crashes.RemoveAll(t => t < windowStart);

            _logger.LogWarning(
                "Module {ModuleName} crash recorded — {CrashCount} crashes in last {WindowMinutes} minutes",
                moduleName,
                crashes.Count,
                _options.CrashLoopWindowMinutes);

            shouldRollback = crashes.Count >= _options.CrashLoopThreshold;
        }

        if (shouldRollback)
        {
            _logger.LogCritical(
                "Crash loop detected for module {ModuleName}: {CrashCount} crashes in {WindowMinutes} minutes — triggering rollback",
                moduleName,
                _options.CrashLoopThreshold,
                _options.CrashLoopWindowMinutes);

            CrashLoopDetected?.Invoke(this, moduleName);

            try
            {
                await _rollback.RollbackToLastGoodAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed for crash loop on module {ModuleName}", moduleName);
            }
        }
    }

    /// <summary>
    /// Gets the number of crashes recorded for a module within the current window.
    /// Exposed for testing.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <returns>Number of recent crashes.</returns>
    public int GetRecentCrashCount(string moduleName)
    {
        if (!_crashHistory.TryGetValue(moduleName, out var crashes))
        {
            return 0;
        }

        var windowStart = DateTimeOffset.UtcNow.AddMinutes(-_options.CrashLoopWindowMinutes);

        lock (_detectionLock)
        {
            crashes.RemoveAll(t => t < windowStart);
            return crashes.Count;
        }
    }
}
