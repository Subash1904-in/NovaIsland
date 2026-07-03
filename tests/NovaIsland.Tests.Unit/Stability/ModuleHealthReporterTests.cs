using FluentAssertions;
using NovaIsland.Application.Stability;
using NovaIsland.Infrastructure.Stability;
using Xunit;

namespace NovaIsland.Tests.Unit.Stability;

/// <summary>
/// Unit tests for <see cref="ModuleHealthReporter"/>.
/// Validates state transitions, event raising, and thread safety.
/// </summary>
public sealed class ModuleHealthReporterTests
{
    /// <summary>
    /// Unknown module should return <see cref="ModuleHealth.NotStarted"/>.
    /// </summary>
    [Fact]
    public void GetHealth_UnknownModule_ReturnsNotStarted()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();

        // Act & Assert.
        reporter.GetHealth("NonExistent").Should().Be(ModuleHealth.NotStarted);
    }

    /// <summary>
    /// Reporting health should store and return the correct state.
    /// </summary>
    [Fact]
    public void ReportHealth_StoresState_GetHealthReturnsIt()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();

        // Act.
        reporter.ReportHealth("TestModule", ModuleHealth.Running);

        // Assert.
        reporter.GetHealth("TestModule").Should().Be(ModuleHealth.Running);
    }

    /// <summary>
    /// State transitions should raise the HealthChanged event with correct args.
    /// </summary>
    [Fact]
    public void ReportHealth_StateChange_RaisesEvent()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();
        ModuleHealthChangedEventArgs? eventArgs = null;
        reporter.HealthChanged += (_, args) => eventArgs = args;

        // Act: first report (NotStarted → Running).
        reporter.ReportHealth("TestModule", ModuleHealth.Running);

        // Assert.
        eventArgs.Should().NotBeNull();
        eventArgs!.ModuleName.Should().Be("TestModule");
        eventArgs.PreviousHealth.Should().Be(ModuleHealth.NotStarted);
        eventArgs.CurrentHealth.Should().Be(ModuleHealth.Running);
    }

    /// <summary>
    /// Reporting the same state should NOT raise an event.
    /// </summary>
    [Fact]
    public void ReportHealth_SameState_DoesNotRaiseEvent()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();
        reporter.ReportHealth("TestModule", ModuleHealth.Running);

        var eventCount = 0;
        reporter.HealthChanged += (_, _) => eventCount++;

        // Act: report same state again.
        reporter.ReportHealth("TestModule", ModuleHealth.Running);

        // Assert.
        eventCount.Should().Be(0, "same state should not trigger an event");
    }

    /// <summary>
    /// GetAllHealth should return a snapshot of all module states.
    /// </summary>
    [Fact]
    public void GetAllHealth_ReturnsSnapshot()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();
        reporter.ReportHealth("ModuleA", ModuleHealth.Running);
        reporter.ReportHealth("ModuleB", ModuleHealth.Degraded);
        reporter.ReportHealth("ModuleC", ModuleHealth.Stopped);

        // Act.
        var snapshot = reporter.GetAllHealth();

        // Assert.
        snapshot.Should().HaveCount(3);
        snapshot["ModuleA"].Should().Be(ModuleHealth.Running);
        snapshot["ModuleB"].Should().Be(ModuleHealth.Degraded);
        snapshot["ModuleC"].Should().Be(ModuleHealth.Stopped);
    }

    /// <summary>
    /// Multiple state transitions should raise events in order.
    /// </summary>
    [Fact]
    public void ReportHealth_MultipleTransitions_RaisesEventsInOrder()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();
        var transitions = new List<(ModuleHealth Previous, ModuleHealth Current)>();
        reporter.HealthChanged += (_, args) =>
            transitions.Add((args.PreviousHealth, args.CurrentHealth));

        // Act.
        reporter.ReportHealth("TestModule", ModuleHealth.Running);
        reporter.ReportHealth("TestModule", ModuleHealth.Restarting);
        reporter.ReportHealth("TestModule", ModuleHealth.Running);
        reporter.ReportHealth("TestModule", ModuleHealth.Degraded);

        // Assert.
        transitions.Should().HaveCount(4);
        transitions[0].Should().Be((ModuleHealth.NotStarted, ModuleHealth.Running));
        transitions[1].Should().Be((ModuleHealth.Running, ModuleHealth.Restarting));
        transitions[2].Should().Be((ModuleHealth.Restarting, ModuleHealth.Running));
        transitions[3].Should().Be((ModuleHealth.Running, ModuleHealth.Degraded));
    }

    /// <summary>
    /// Concurrent updates should not corrupt state.
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdates_DoNotCorruptState()
    {
        // Arrange.
        var reporter = new ModuleHealthReporter();
        var tasks = new List<Task>();

        // Act: fire 100 concurrent updates across 10 modules.
        for (var i = 0; i < 100; i++)
        {
            var moduleName = $"Module{i % 10}";
            var health = (ModuleHealth)(i % 5);
            tasks.Add(Task.Run(() => reporter.ReportHealth(moduleName, health)));
        }

        await Task.WhenAll(tasks);

        // Assert: all 10 modules should be tracked.
        var allHealth = reporter.GetAllHealth();
        allHealth.Should().HaveCount(10);

        // Each module should have a valid health state.
        foreach (var (_, health) in allHealth)
        {
            health.Should().BeOneOf(
                ModuleHealth.NotStarted,
                ModuleHealth.Running,
                ModuleHealth.Restarting,
                ModuleHealth.Degraded,
                ModuleHealth.Stopped);
        }
    }
}
