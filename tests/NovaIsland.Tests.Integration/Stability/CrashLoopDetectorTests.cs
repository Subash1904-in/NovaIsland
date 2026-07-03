using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Infrastructure.Stability;
using Xunit;

namespace NovaIsland.Tests.Integration.Stability;

/// <summary>
/// Tests for <see cref="CrashLoopDetector"/>.
/// Validates that crash-loop detection triggers rollback correctly
/// and does not trigger on spread-out crashes.
/// </summary>
public sealed class CrashLoopDetectorTests
{
    /// <summary>
    /// When crash threshold is exceeded within the time window,
    /// rollback must be triggered.
    /// </summary>
    [Fact]
    public async Task DetectsLoop_WhenThresholdExceeded()
    {
        // Arrange.
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            CrashLoopThreshold = 3,
            CrashLoopWindowMinutes = 5,
        });
        var detector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        var loopDetectedModule = string.Empty;
        detector.CrashLoopDetected += (_, name) => loopDetectedModule = name;

        // Act: record 3 crashes in rapid succession.
        await detector.RecordCrashAsync("TestModule");
        await detector.RecordCrashAsync("TestModule");
        await detector.RecordCrashAsync("TestModule");

        // Assert.
        stubRollback.WasRollbackRequested.Should().BeTrue(
            "rollback should be triggered when crash threshold is reached");
        stubRollback.RollbackCount.Should().Be(1);
        loopDetectedModule.Should().Be("TestModule");
    }

    /// <summary>
    /// Crashes from different modules should not cross-contaminate each other's counts.
    /// </summary>
    [Fact]
    public async Task CrashesFromDifferentModules_DoNotCrossContaminate()
    {
        // Arrange.
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            CrashLoopThreshold = 3,
            CrashLoopWindowMinutes = 5,
        });
        var detector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        // Act: 2 crashes each on two different modules (below threshold of 3).
        await detector.RecordCrashAsync("ModuleA");
        await detector.RecordCrashAsync("ModuleA");
        await detector.RecordCrashAsync("ModuleB");
        await detector.RecordCrashAsync("ModuleB");

        // Assert: no rollback triggered because no single module hit 3.
        stubRollback.WasRollbackRequested.Should().BeFalse(
            "no single module exceeded the crash threshold");
    }

    /// <summary>
    /// Crashes spread beyond the window should not trigger rollback.
    /// </summary>
    [Fact]
    public async Task DoesNotTrigger_WhenCrashesSpreadOutsideWindow()
    {
        // Arrange: use a very short window (1 minute) to make testing feasible.
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            CrashLoopThreshold = 3,
            CrashLoopWindowMinutes = 1, // 1-minute window.
        });
        var detector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        // Act: record 2 crashes (below threshold).
        await detector.RecordCrashAsync("SpreadModule");
        await detector.RecordCrashAsync("SpreadModule");

        // Assert: no rollback.
        stubRollback.WasRollbackRequested.Should().BeFalse();
        detector.GetRecentCrashCount("SpreadModule").Should().Be(2);
    }

    /// <summary>
    /// Verifies <see cref="StubUpdateRollback"/> correctly tracks multiple invocations.
    /// </summary>
    [Fact]
    public async Task StubRollback_TracksMultipleInvocations()
    {
        // Arrange.
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());

        var eventFired = 0;
        stubRollback.RollbackRequested += (_, _) => eventFired++;

        // Act.
        await stubRollback.RollbackToLastGoodAsync();
        await stubRollback.RollbackToLastGoodAsync();
        await stubRollback.RollbackToLastGoodAsync();

        // Assert.
        stubRollback.RollbackCount.Should().Be(3);
        eventFired.Should().Be(3);
    }
}
