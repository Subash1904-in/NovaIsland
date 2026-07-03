namespace NovaIsland.Application.Stability;

/// <summary>
/// Reports and tracks the health state of all supervised modules.
/// Thread-safe. Supports event-driven observation of state transitions.
/// </summary>
public interface IModuleHealthReporter
{
    /// <summary>
    /// Raised whenever a module transitions to a new health state.
    /// </summary>
    event EventHandler<ModuleHealthChangedEventArgs>? HealthChanged;

    /// <summary>
    /// Gets the current health state of the specified module.
    /// Returns <see cref="ModuleHealth.NotStarted"/> if the module is unknown.
    /// </summary>
    /// <param name="moduleName">The module's unique name.</param>
    /// <returns>The current health state.</returns>
    ModuleHealth GetHealth(string moduleName);

    /// <summary>
    /// Sets the health state of the specified module, raising
    /// <see cref="HealthChanged"/> if the state actually changed.
    /// </summary>
    /// <param name="moduleName">The module's unique name.</param>
    /// <param name="health">The new health state.</param>
    void ReportHealth(string moduleName, ModuleHealth health);

    /// <summary>
    /// Gets a snapshot of all known module health states.
    /// </summary>
    /// <returns>Dictionary mapping module name to health state.</returns>
    IReadOnlyDictionary<string, ModuleHealth> GetAllHealth();
}

/// <summary>
/// Event arguments for module health state transitions.
/// </summary>
/// <param name="ModuleName">The name of the module whose state changed.</param>
/// <param name="PreviousHealth">The previous health state.</param>
/// <param name="CurrentHealth">The new health state.</param>
#pragma warning disable CA1711 // EventArgs suffix is the correct .NET convention for event argument types
public sealed record ModuleHealthChangedEventArgs(
    string ModuleName,
    ModuleHealth PreviousHealth,
    ModuleHealth CurrentHealth);
#pragma warning restore CA1711
