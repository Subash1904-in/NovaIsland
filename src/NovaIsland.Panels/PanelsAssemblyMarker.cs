namespace NovaIsland.Panels;

/// <summary>
/// Marker file for the Panels layer.
/// Contains WinUI 3 secondary panels (settings, marketplace, widget gallery).
/// XAML is permitted here — these are not on the hot-path (see SRS §6).
/// </summary>
public static class PanelsAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(PanelsAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.Panels";
}
