using NovaIsland.UI.Animation;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Configuration settings for the Nova Island shell, bound from the "Island" section
/// of appsettings.json. Controls spring physics, initial state, dimensions, and
/// accessibility options.
/// </summary>
/// <remarks>
/// These settings are read once at startup and used to configure the animation system.
/// Changes require application restart (hot-reload not supported for shell config).
/// </remarks>
public sealed class IslandSettings
{
    /// <summary>
    /// Spring stiffness coefficient. Higher values produce faster animations.
    /// Default: 300.0 (with critical damping ≈ 34.64).
    /// </summary>
    public float Stiffness { get; set; } = 300f;

    /// <summary>
    /// Spring damping coefficient. Set to 0 to auto-compute critical damping
    /// (2 × √stiffness). Positive values override.
    /// </summary>
    public float Damping { get; set; }

    /// <summary>
    /// When true, replaces spring animations with instant transitions and
    /// short opacity cross-fades for accessibility (reduced-motion mode).
    /// </summary>
    public bool ReducedMotion { get; set; }

    /// <summary>
    /// The initial state of the island shell on startup.
    /// </summary>
    public IslandState InitialState { get; set; } = IslandState.Compact;

    /// <summary>Compact state width in logical pixels.</summary>
    public float CompactWidth { get; set; } = 160f;

    /// <summary>Compact state height in logical pixels.</summary>
    public float CompactHeight { get; set; } = 16f;

    /// <summary>Expanded state width in logical pixels.</summary>
    public float ExpandedWidth { get; set; } = 400f;

    /// <summary>Expanded state height in logical pixels.</summary>
    public float ExpandedHeight { get; set; } = 320f;

    /// <summary>Minimal state width in logical pixels.</summary>
    public float MinimalWidth { get; set; } = 120f;

    /// <summary>Minimal state height in logical pixels.</summary>
    public float MinimalHeight { get; set; } = 8f;

    /// <summary>Alert state width in logical pixels.</summary>
    public float AlertWidth { get; set; } = 300f;

    /// <summary>Alert state height in logical pixels.</summary>
    public float AlertHeight { get; set; } = 60f;

    /// <summary>
    /// Gets the effective spring configuration, auto-computing critical damping
    /// when <see cref="Damping"/> is zero.
    /// </summary>
    /// <returns>A <see cref="SpringConfig"/> instance.</returns>
    public SpringConfig GetSpringConfig()
    {
        return Damping > 0f
            ? new SpringConfig(Stiffness, Damping)
            : SpringConfig.CriticallyDamped(Stiffness);
    }

    /// <summary>
    /// Applies configured dimension overrides to the state descriptor table.
    /// Called once at startup before any animation begins.
    /// </summary>
    public void ApplyDimensionOverrides()
    {
        IslandStateDescriptors.SetDescriptor(IslandState.Compact,
            new IslandStateDescriptor(CompactWidth, CompactHeight, 16f, 1.0f, 0f));
        IslandStateDescriptors.SetDescriptor(IslandState.Expanded,
            new IslandStateDescriptor(ExpandedWidth, ExpandedHeight, 16f, 1.0f, 0f));
        IslandStateDescriptors.SetDescriptor(IslandState.Minimal,
            new IslandStateDescriptor(MinimalWidth, MinimalHeight, 4f, 0.7f, 0f));
        IslandStateDescriptors.SetDescriptor(IslandState.Alert,
            new IslandStateDescriptor(AlertWidth, AlertHeight, 14f, 1.0f, 0f));
    }
}
