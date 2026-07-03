namespace NovaIsland.SDK;

/// <summary>
/// Marker file for the Plugin SDK.
/// Contains the public API surface for third-party plugin development.
/// </summary>
public static class SdkAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(SdkAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.SDK";
}
