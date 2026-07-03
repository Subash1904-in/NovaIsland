namespace NovaIsland.Application.Stability;

/// <summary>
/// Lifecycle states for a supervised module within the Nova Island shell.
/// </summary>
public enum ModuleHealth
{
    /// <summary>
    /// The module has not yet been started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// The module is executing normally.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The module crashed and is being restarted with exponential backoff.
    /// </summary>
    Restarting = 2,

    /// <summary>
    /// The module has exhausted its retry budget and is no longer being restarted.
    /// The island shell continues without it.
    /// </summary>
    Degraded = 3,

    /// <summary>
    /// The module was gracefully stopped (e.g., during host shutdown).
    /// </summary>
    Stopped = 4,
}
