namespace NovaIsland.Plugins.Host;

/// <summary>
/// Marker file for the Plugin Host.
/// Contains the sandboxed plugin runtime — child process isolation (default)
/// and WASM execution (Wasmtime) for untrusted plugins. See SRS §7.
/// </summary>
public static class PluginHostAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(PluginHostAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.Plugins.Host";
}
