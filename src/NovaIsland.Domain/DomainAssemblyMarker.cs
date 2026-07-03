namespace NovaIsland.Domain;

/// <summary>
/// Marker file for the Domain layer.
/// Contains entities, value objects, domain rules, and automation DSL definitions.
/// </summary>
public static class DomainAssemblyMarker
{
    /// <summary>
    /// Gets the assembly name for reflection-free assembly identification.
    /// </summary>
    public static string AssemblyName => typeof(DomainAssemblyMarker).Assembly.GetName().Name
        ?? "NovaIsland.Domain";
}
