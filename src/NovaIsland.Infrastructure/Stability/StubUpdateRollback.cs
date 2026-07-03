using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Stub implementation of <see cref="IUpdateRollback"/> for use before Velopack is wired.
/// Logs the rollback request and records that it was invoked for testability.
/// </summary>
public sealed class StubUpdateRollback : IUpdateRollback
{
    private readonly ILogger<StubUpdateRollback> _logger;
    private int _rollbackCount;

    /// <summary>
    /// Gets the number of times rollback has been invoked. Exposed for testing.
    /// </summary>
    public int RollbackCount => _rollbackCount;

    /// <summary>
    /// Gets a value indicating whether rollback was ever invoked.
    /// </summary>
    public bool WasRollbackRequested => _rollbackCount > 0;

    /// <summary>
    /// Raised when a rollback is requested. Exposed for testing.
    /// </summary>
    public event EventHandler? RollbackRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubUpdateRollback"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public StubUpdateRollback(ILogger<StubUpdateRollback> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RollbackToLastGoodAsync()
    {
        Interlocked.Increment(ref _rollbackCount);

        _logger.LogCritical(
            "[STUB] Velopack rollback to last-known-good version requested (invocation #{Count}). " +
            "This is a stub — actual Velopack integration is pending.",
            _rollbackCount);

        RollbackRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
