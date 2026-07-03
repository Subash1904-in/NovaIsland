namespace NovaIsland.Application.Stability;

/// <summary>
/// Contract for a Nova Island module that runs inside a supervision boundary.
/// Each module is isolated: an unhandled exception restarts only the faulted module,
/// never the entire application host.
/// </summary>
public interface INovaModule
{
    /// <summary>
    /// Gets the unique name of this module, used for logging, health reporting,
    /// and crash-loop tracking.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Executes the module's main work loop. Called by <c>SupervisedModuleService</c>.
    /// Implementations should run until <paramref name="cancellationToken"/> is cancelled.
    /// Any unhandled exception triggers the supervision boundary's restart logic.
    /// </summary>
    /// <param name="cancellationToken">Token cancelled when the module should stop.</param>
    /// <returns>A task that completes when the module stops.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
