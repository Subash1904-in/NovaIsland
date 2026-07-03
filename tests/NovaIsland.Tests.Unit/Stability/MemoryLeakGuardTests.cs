using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Infrastructure.Stability;
using Xunit;

namespace NovaIsland.Tests.Unit.Stability;

/// <summary>
/// Unit tests for <see cref="MemoryLeakGuardService"/>.
/// Validates diagnostics logging thresholds and idle debounce logic.
/// </summary>
public sealed class MemoryLeakGuardTests
{
    /// <summary>
    /// When not idle long enough, working set should NOT be trimmed.
    /// </summary>
    [Fact]
    public void SampleDiagnostics_NotIdleLongEnough_DoesNotTrim()
    {
        // Arrange.
        var options = Options.Create(new StabilityOptions
        {
            WorkingSetCheckIntervalSeconds = 1,
            IdleTrimDebounceSeconds = 3600, // 1 hour — ensures idle debounce is never met.
            WorkingSetWarnThresholdMb = 999, // Very high — no warnings.
            HandleCountWarnThreshold = 99999,
        });

        var guard = new MemoryLeakGuardService(
            options,
            new LoggerFactory().CreateLogger<MemoryLeakGuardService>());

        var trimmed = false;
        guard.WorkingSetTrimmed += (_, _) => trimmed = true;

        // Inject recent activity to prevent idle detection.
        guard.LastActivityProvider = () => DateTimeOffset.UtcNow;

        // Act.
        guard.SampleDiagnostics();

        // Assert.
        trimmed.Should().BeFalse("working set should not be trimmed when not idle");
    }

    /// <summary>
    /// When idle for longer than the debounce period, working set SHOULD be trimmed.
    /// </summary>
    [Fact]
    public void SampleDiagnostics_IdleBeyondDebounce_TrimsWorkingSet()
    {
        // Arrange.
        var options = Options.Create(new StabilityOptions
        {
            WorkingSetCheckIntervalSeconds = 1,
            IdleTrimDebounceSeconds = 1, // 1 second idle threshold.
            WorkingSetWarnThresholdMb = 999,
            HandleCountWarnThreshold = 99999,
        });

        var guard = new MemoryLeakGuardService(
            options,
            new LoggerFactory().CreateLogger<MemoryLeakGuardService>());

        var trimmed = false;
        guard.WorkingSetTrimmed += (_, _) => trimmed = true;

        // Inject activity timestamp from the distant past to simulate idle.
        guard.LastActivityProvider = () => DateTimeOffset.UtcNow.AddMinutes(-10);

        // Act.
        guard.SampleDiagnostics();

        // Assert.
        trimmed.Should().BeTrue("working set should be trimmed after idle debounce");
    }

    /// <summary>
    /// RecordActivity resets the idle timer so trimming doesn't occur.
    /// </summary>
    [Fact]
    public void RecordActivity_ResetsIdleTimer()
    {
        // Arrange.
        var options = Options.Create(new StabilityOptions
        {
            WorkingSetCheckIntervalSeconds = 1,
            IdleTrimDebounceSeconds = 1,
            WorkingSetWarnThresholdMb = 999,
            HandleCountWarnThreshold = 99999,
        });

        var guard = new MemoryLeakGuardService(
            options,
            new LoggerFactory().CreateLogger<MemoryLeakGuardService>());

        var trimmed = false;
        guard.WorkingSetTrimmed += (_, _) => trimmed = true;

        // Simulate activity just now (no provider injection — uses internal timestamp).
        guard.RecordActivity();

        // Act.
        guard.SampleDiagnostics();

        // Assert.
        trimmed.Should().BeFalse(
            "recent activity should prevent trimming");
    }
}
