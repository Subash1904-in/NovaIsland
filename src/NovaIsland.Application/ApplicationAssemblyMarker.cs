namespace NovaIsland.Application;

/// <summary>
/// Marker file for the Application layer.
/// Contains use cases, orchestrators, and MVVM view model abstractions.
/// </summary>
public static class ApplicationAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(ApplicationAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.Application";
}
