using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Application.Stability;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Hosted service that wraps a single <see cref="INovaModule"/> in a supervision boundary.
/// On unhandled exception: logs via Serilog, applies exponential backoff, and restarts the module.
/// After exhausting <see cref="StabilityOptions.ModuleMaxRetries"/>, marks the module as
/// <see cref="ModuleHealth.Degraded"/> and stops retrying — the shell stays alive.
/// </summary>
public sealed class SupervisedModuleService : IHostedService, IDisposable
{
    private readonly INovaModule _module;
    private readonly IModuleHealthReporter _healthReporter;
    private readonly CrashLoopDetector _crashLoopDetector;
    private readonly StabilityOptions _options;
    private readonly ILogger<SupervisedModuleService> _logger;

    private CancellationTokenSource? _moduleCts;
    private Task? _executionTask;
    private bool _disposed;

    /// <summary>
    /// The base delay for exponential backoff (500 ms → 1 s → 2 s → 4 s → 8 s).
    /// </summary>
    internal static readonly TimeSpan BaseBackoffDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum backoff cap: 8 seconds.
    /// </summary>
    internal static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisedModuleService"/> class.
    /// </summary>
    /// <param name="module">The module to supervise.</param>
    /// <param name="healthReporter">Reports module health transitions.</param>
    /// <param name="crashLoopDetector">Detects crash loops and triggers rollback.</param>
    /// <param name="options">Stability configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public SupervisedModuleService(
        INovaModule module,
        IModuleHealthReporter healthReporter,
        CrashLoopDetector crashLoopDetector,
        IOptions<StabilityOptions> options,
        ILogger<SupervisedModuleService> logger)
    {
        _module = module;
        _healthReporter = healthReporter;
        _crashLoopDetector = crashLoopDetector;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting supervised module: {ModuleName}", _module.ModuleName);
        _moduleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionTask = RunSupervisedAsync(_moduleCts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping supervised module: {ModuleName}", _module.ModuleName);

        if (_moduleCts is not null)
        {
            await _moduleCts.CancelAsync().ConfigureAwait(false);
        }

        if (_executionTask is not null)
        {
            // Wait for the module to stop, but don't wait forever.
            await Task.WhenAny(_executionTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken))
                .ConfigureAwait(false);
        }

        _healthReporter.ReportHealth(_module.ModuleName, ModuleHealth.Stopped);
    }

    /// <summary>
    /// Core supervision loop: runs the module, catches unhandled exceptions,
    /// applies exponential backoff, and retries up to the configured maximum.
    /// </summary>
    private async Task RunSupervisedAsync(CancellationToken cancellationToken)
    {
        var restartCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _healthReporter.ReportHealth(_module.ModuleName, ModuleHealth.Running);
                _logger.LogInformation(
                    "Module {ModuleName} executing (attempt {Attempt})",
                    _module.ModuleName,
                    restartCount + 1);

                await _module.ExecuteAsync(cancellationToken).ConfigureAwait(false);

                // If ExecuteAsync returns normally, the module completed its work.
                _logger.LogInformation("Module {ModuleName} completed normally", _module.ModuleName);
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Module {ModuleName} cancelled during shutdown", _module.ModuleName);
                break;
            }
            catch (Exception ex)
            {
                restartCount++;

                _logger.LogError(
                    ex,
                    "Module {ModuleName} threw unhandled exception (crash #{CrashCount})",
                    _module.ModuleName,
                    restartCount);

                // Record crash for crash-loop detection.
                await _crashLoopDetector.RecordCrashAsync(_module.ModuleName).ConfigureAwait(false);

                if (restartCount >= _options.ModuleMaxRetries)
                {
                    _logger.LogCritical(
                        "Module {ModuleName} exceeded max retries ({MaxRetries}) — marking degraded",
                        _module.ModuleName,
                        _options.ModuleMaxRetries);

                    _healthReporter.ReportHealth(_module.ModuleName, ModuleHealth.Degraded);
                    break;
                }

                // Exponential backoff: 500ms, 1s, 2s, 4s, 8s (capped).
                var backoffDelay = CalculateBackoff(restartCount);

                _healthReporter.ReportHealth(_module.ModuleName, ModuleHealth.Restarting);
                _logger.LogWarning(
                    "Module {ModuleName} will restart in {BackoffMs}ms (attempt {Attempt}/{MaxRetries})",
                    _module.ModuleName,
                    backoffDelay.TotalMilliseconds,
                    restartCount + 1,
                    _options.ModuleMaxRetries);

                try
                {
                    await Task.Delay(backoffDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Calculates exponential backoff delay based on the restart count.
    /// </summary>
    /// <param name="restartCount">Number of restarts so far (1-based).</param>
    /// <returns>The backoff delay, capped at <see cref="MaxBackoffDelay"/>.</returns>
    internal static TimeSpan CalculateBackoff(int restartCount)
    {
        // 2^(n-1) * base: 500ms, 1s, 2s, 4s, 8s, 8s, 8s...
        var multiplier = Math.Pow(2, restartCount - 1);
        var delay = TimeSpan.FromMilliseconds(BaseBackoffDelay.TotalMilliseconds * multiplier);
        return delay > MaxBackoffDelay ? MaxBackoffDelay : delay;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _moduleCts?.Cancel();
        _moduleCts?.Dispose();
    }
}
