namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Configuration options for the stability subsystem.
/// Bound to the <c>Stability</c> section in <c>appsettings.json</c>.
/// </summary>
public sealed class StabilityOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Stability";

    /// <summary>
    /// Gets or sets the heartbeat interval in milliseconds.
    /// The main app sends a heartbeat to the watchdog at this rate.
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the named-pipe name for the watchdog heartbeat protocol.
    /// </summary>
    public string HeartbeatPipeName { get; set; } = "NovaIsland_Heartbeat";

    /// <summary>
    /// Gets or sets the number of consecutive missed heartbeats before
    /// the watchdog restarts the main app.
    /// </summary>
    public int MissedHeartbeatsForRestart { get; set; } = 4;

    /// <summary>
    /// Gets or sets the interval (seconds) between memory/handle diagnostic samples.
    /// </summary>
    public int WorkingSetCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the idle debounce duration (seconds) before working-set trimming.
    /// </summary>
    public int IdleTrimDebounceSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the working-set warning threshold in megabytes.
    /// </summary>
    public int WorkingSetWarnThresholdMb { get; set; } = 35;

    /// <summary>
    /// Gets or sets the handle count warning threshold.
    /// </summary>
    public int HandleCountWarnThreshold { get; set; } = 500;

    /// <summary>
    /// Gets or sets the number of crashes within <see cref="CrashLoopWindowMinutes"/>
    /// that triggers a rollback.
    /// </summary>
    public int CrashLoopThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the time window (minutes) for crash-loop detection.
    /// </summary>
    public int CrashLoopWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of restart retries per module
    /// before marking it as degraded.
    /// </summary>
    public int ModuleMaxRetries { get; set; } = 5;
}
