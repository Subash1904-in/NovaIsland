using NovaIsland.Application.Stability;

namespace NovaIsland.Tests.Integration.Stability;

/// <summary>
/// Test module that throws an exception after a configurable number of executions.
/// Used to verify supervision boundary behavior.
/// </summary>
internal sealed class FaultyModule : INovaModule
{
    private int _executionCount;
    private readonly int _throwAfterExecutions;
    private readonly string _moduleName;

    /// <summary>
    /// Gets the number of times <see cref="ExecuteAsync"/> has been called.
    /// </summary>
    public int ExecutionCount => _executionCount;

    /// <inheritdoc />
    public string ModuleName => _moduleName;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaultyModule"/> class.
    /// </summary>
    /// <param name="moduleName">Module name for identification.</param>
    /// <param name="throwAfterExecutions">Number of executions before throwing. 0 = throw immediately.</param>
    public FaultyModule(string moduleName = "FaultyModule", int throwAfterExecutions = 0)
    {
        _moduleName = moduleName;
        _throwAfterExecutions = throwAfterExecutions;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var count = Interlocked.Increment(ref _executionCount);

        if (count > _throwAfterExecutions)
        {
            throw new InvalidOperationException(
                $"[{_moduleName}] Intentional fault at execution #{count}");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Test module that runs successfully, incrementing a counter on each tick.
/// Used to verify that healthy modules continue running when others crash.
/// </summary>
internal sealed class HealthyModule : INovaModule
{
    private int _tickCount;

    /// <summary>
    /// Gets the number of successful ticks.
    /// </summary>
    public int TickCount => _tickCount;

    /// <inheritdoc />
    public string ModuleName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthyModule"/> class.
    /// </summary>
    /// <param name="moduleName">Module name for identification.</param>
    public HealthyModule(string moduleName = "HealthyModule")
    {
        ModuleName = moduleName;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _tickCount);
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }
}
