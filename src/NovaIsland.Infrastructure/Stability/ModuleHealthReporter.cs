using System.Collections.Concurrent;
using NovaIsland.Application.Stability;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Thread-safe implementation of <see cref="IModuleHealthReporter"/>.
/// Stores per-module health states and raises events on transitions.
/// </summary>
public sealed class ModuleHealthReporter : IModuleHealthReporter
{
    private readonly ConcurrentDictionary<string, ModuleHealth> _healthStates = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event EventHandler<ModuleHealthChangedEventArgs>? HealthChanged;

    /// <inheritdoc />
    public ModuleHealth GetHealth(string moduleName)
    {
        return _healthStates.TryGetValue(moduleName, out var health)
            ? health
            : ModuleHealth.NotStarted;
    }

    /// <inheritdoc />
    public void ReportHealth(string moduleName, ModuleHealth health)
    {
        var previous = ModuleHealth.NotStarted;

        _healthStates.AddOrUpdate(
            moduleName,
            addValueFactory: _ => health,
            updateValueFactory: (_, existing) =>
            {
                previous = existing;
                return health;
            });

        // If key was newly added, previous remains NotStarted.
        // If key was updated, previous was captured by the updateValueFactory.
        if (previous != health)
        {
            HealthChanged?.Invoke(this, new ModuleHealthChangedEventArgs(moduleName, previous, health));
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ModuleHealth> GetAllHealth()
    {
        return new Dictionary<string, ModuleHealth>(_healthStates, StringComparer.Ordinal);
    }
}
