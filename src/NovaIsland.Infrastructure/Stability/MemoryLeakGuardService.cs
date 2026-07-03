using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Background service that periodically samples the process's working set and handle count.
/// Logs warnings when thresholds are exceeded and trims the working set after a configurable
/// idle debounce period using <see cref="NativeMethods.SetProcessWorkingSetSize"/>.
/// </summary>
public sealed class MemoryLeakGuardService : BackgroundService
{
    private readonly StabilityOptions _options;
    private readonly ILogger<MemoryLeakGuardService> _logger;
    private DateTimeOffset _lastActivityTime;

    /// <summary>
    /// Raised when working set is trimmed. Exposed for testing.
    /// </summary>
    public event EventHandler? WorkingSetTrimmed;

    /// <summary>
    /// Allows injection of a custom activity tracker for testing.
    /// When null, uses <see cref="DateTimeOffset.UtcNow"/> as last activity.
    /// </summary>
    internal Func<DateTimeOffset>? LastActivityProvider { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryLeakGuardService"/> class.
    /// </summary>
    /// <param name="options">Stability configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public MemoryLeakGuardService(
        IOptions<StabilityOptions> options,
        ILogger<MemoryLeakGuardService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _lastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records user activity to reset the idle debounce timer.
    /// Call this from input handlers (mouse, keyboard, touch).
    /// </summary>
    public void RecordActivity()
    {
        _lastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MemoryLeakGuard starting — check interval: {IntervalSec}s, idle trim debounce: {IdleSec}s",
            _options.WorkingSetCheckIntervalSeconds,
            _options.IdleTrimDebounceSeconds);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.WorkingSetCheckIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            SampleDiagnostics();
        }

        _logger.LogInformation("MemoryLeakGuard stopped");
    }

    /// <summary>
    /// Takes a diagnostic sample: reads working set and handle count,
    /// logs warnings, and trims working set if idle.
    /// </summary>
    internal void SampleDiagnostics()
    {
        using var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
        var handleCount = process.HandleCount;

        _logger.LogDebug(
            "Diagnostics — WorkingSet: {WorkingSetMb:F1} MB, Handles: {HandleCount}",
            workingSetMb,
            handleCount);

        if (workingSetMb > _options.WorkingSetWarnThresholdMb)
        {
            _logger.LogWarning(
                "Working set {WorkingSetMb:F1} MB exceeds threshold {ThresholdMb} MB",
                workingSetMb,
                _options.WorkingSetWarnThresholdMb);
        }

        if (handleCount > _options.HandleCountWarnThreshold)
        {
            _logger.LogWarning(
                "Handle count {HandleCount} exceeds threshold {Threshold}",
                handleCount,
                _options.HandleCountWarnThreshold);
        }

        // Check if idle long enough to trim.
        var lastActivity = LastActivityProvider?.Invoke() ?? _lastActivityTime;
        var idleDuration = DateTimeOffset.UtcNow - lastActivity;
        var debounce = TimeSpan.FromSeconds(_options.IdleTrimDebounceSeconds);

        if (idleDuration >= debounce)
        {
            TrimWorkingSet();
        }
    }

    /// <summary>
    /// Trims the working set to its minimum via <c>SetProcessWorkingSetSize(-1, -1)</c>.
    /// </summary>
    private void TrimWorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var before = process.WorkingSet64 / (1024.0 * 1024.0);

            var success = NativeMethods.SetProcessWorkingSetSize(
                process.Handle,
                (nint)(-1),
                (nint)(-1));

            if (success)
            {
                process.Refresh();
                var after = process.WorkingSet64 / (1024.0 * 1024.0);
                _logger.LogInformation(
                    "Working set trimmed after idle: {BeforeMb:F1} MB → {AfterMb:F1} MB",
                    before,
                    after);

                WorkingSetTrimmed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _logger.LogWarning("SetProcessWorkingSetSize returned false");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trim working set");
        }
    }
}
