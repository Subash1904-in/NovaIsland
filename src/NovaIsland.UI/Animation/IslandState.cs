namespace NovaIsland.UI.Animation;

/// <summary>
/// The visual states of the Nova Island shell.
/// Each state defines a distinct shape, size, and visual appearance.
/// Transitions between states are driven by spring animations (or instant cross-fades in reduced-motion mode).
/// </summary>
public enum IslandState
{
    /// <summary>Compact pill shape at top-center of screen. Default idle state.</summary>
    Compact = 0,

    /// <summary>Expanded content area showing full module UI.</summary>
    Expanded = 1,

    /// <summary>Minimal thin accent line, nearly hidden.</summary>
    Minimal = 2,

    /// <summary>Alert notification banner, wider than compact but shorter than expanded.</summary>
    Alert = 3,
}

/// <summary>
/// Describes the visual properties of an <see cref="IslandState"/>.
/// This is a readonly struct to avoid heap allocation in the animation hot path.
/// </summary>
public readonly struct IslandStateDescriptor
{
    /// <summary>Width of the island in logical pixels.</summary>
    public readonly float Width;

    /// <summary>Height of the island in logical pixels.</summary>
    public readonly float Height;

    /// <summary>Corner radius for rounded rectangle shape.</summary>
    public readonly float CornerRadius;

    /// <summary>Opacity of the island (0.0–1.0).</summary>
    public readonly float Opacity;

    /// <summary>Vertical offset from the top of the screen in logical pixels.</summary>
    public readonly float OffsetY;

    /// <summary>
    /// Initializes a new <see cref="IslandStateDescriptor"/>.
    /// </summary>
    public IslandStateDescriptor(float width, float height, float cornerRadius, float opacity, float offsetY)
    {
        Width = width;
        Height = height;
        CornerRadius = cornerRadius;
        Opacity = opacity;
        OffsetY = offsetY;
    }
}

/// <summary>
/// Provides <see cref="IslandStateDescriptor"/> lookup for each <see cref="IslandState"/>.
/// Uses a fixed-size array indexed by enum ordinal for zero-allocation access.
/// </summary>
public static class IslandStateDescriptors
{
    // Pre-allocated array indexed by IslandState ordinal. Never modified after init.
    private static readonly IslandStateDescriptor[] Descriptors =
    [
        new(220f, 40f, 20f, 1.0f, 0f),    // Compact
        new(400f, 320f, 16f, 1.0f, 0f),   // Expanded
        new(120f, 8f, 4f, 0.7f, 0f),      // Minimal
        new(300f, 60f, 14f, 1.0f, 0f),    // Alert
    ];

    /// <summary>
    /// Gets the visual descriptor for the specified state.
    /// Zero-allocation: returns by readonly reference from a pre-allocated array.
    /// </summary>
    /// <param name="state">The island state to look up.</param>
    /// <returns>The descriptor defining the visual properties for that state.</returns>
    public static ref readonly IslandStateDescriptor GetDescriptor(IslandState state)
    {
        return ref Descriptors[(int)state];
    }

    /// <summary>
    /// Updates the descriptor for a state. Used during configuration binding
    /// to apply user-defined dimensions from appsettings.json.
    /// Must only be called during startup, never on the hot path.
    /// </summary>
    /// <param name="state">The state to update.</param>
    /// <param name="descriptor">The new descriptor values.</param>
    public static void SetDescriptor(IslandState state, in IslandStateDescriptor descriptor)
    {
        Descriptors[(int)state] = descriptor;
    }
}
