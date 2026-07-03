namespace NovaIsland.UI;

/// <summary>
/// Marker file for the UI layer.
/// Contains the Win32 + Windows.UI.Composition island shell (hot-path rendering).
/// XAML is banned from this project — see SRS §6.
/// </summary>
public static class UiAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(UiAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.UI";
}
