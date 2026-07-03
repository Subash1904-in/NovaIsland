namespace NovaIsland.Application.Stability;

/// <summary>
/// Contract for rolling back the application to the last known-good version.
/// Triggered by the crash-loop detector when a module exceeds the crash threshold.
/// </summary>
/// <remarks>
/// The default implementation is a stub that logs the rollback request.
/// The actual Velopack integration will replace this in a later phase.
/// </remarks>
public interface IUpdateRollback
{
    /// <summary>
    /// Initiates a rollback to the last known-good application version.
    /// </summary>
    /// <returns>A task that completes when the rollback has been initiated.</returns>
    Task RollbackToLastGoodAsync();
}
