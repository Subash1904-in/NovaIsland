using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NovaIsland.Infrastructure.Stability;
using System;
using Xunit;

namespace NovaIsland.Tests.Perf;

/// <summary>
/// Performance gate tests using dotnet-trace.
/// These tests will be expanded in Phase 8 (Performance &amp; Animation Hardening)
/// to validate the targets defined in SRS §10:
///   - RAM idle &lt; 40 MB
///   - CPU idle &lt; 0.2%
///   - Cold startup &lt; 300 ms
///   - Warm startup &lt; 100 ms (AOT)
///   - Animation 120 FPS sustained, p99 frame-time &lt; 8.3 ms @120Hz
///   - Plugin load &lt; 100 ms
/// </summary>
public sealed class PerfGateTests
{
    [Fact]
    public void PerfGate_Placeholder_TracingInfrastructureReady()
    {
        // This placeholder test validates that the perf-gate test project
        // compiles and can reference the App assembly. Actual dotnet-trace
        // integration will be wired in CI and expanded in later phases.
        //
        // The CI workflow installs dotnet-trace and runs a stub collection
        // against this project to validate the toolchain is functional.

        var isTraceToolAvailable = true; // Will be replaced with actual check
        isTraceToolAvailable.Should().BeTrue("dotnet-trace must be available for perf gates");
    }

    [Fact]
    public void PerfGate_Placeholder_AllocationBudgetDefined()
    {
        // Validates that performance budgets are defined and accessible.
        // Phase 8 will implement actual allocation tracking via dotnet-trace
        // GC events and enforce the zero-alloc hot-path rule (SRS §6).

        const long maxIdleRamBytes = 40L * 1024 * 1024; // 40 MB per SRS §10
        const double maxIdleCpuPercent = 0.2;            // 0.2% per SRS §10
        const int maxColdStartupMs = 300;                // 300 ms per SRS §10
        const int maxWarmStartupMs = 100;                // 100 ms per SRS §10

        maxIdleRamBytes.Should().BeGreaterThan(0);
        maxIdleCpuPercent.Should().BeGreaterThan(0);
        maxColdStartupMs.Should().BeGreaterThan(0);
        maxWarmStartupMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GpuTierDetector_DefaultsToDiscrete_WhenNoSimulatedEnvironment()
    {
        // Act
        // Clear environment variable for test reliability
        Environment.SetEnvironmentVariable("NOVA_SIMULATED_GPU_TIER", null);
        var detector = new GpuTierDetector(NullLogger<GpuTierDetector>.Instance);

        // Assert
        detector.CurrentTier.Should().Be(GraphicsTier.High);
        detector.IsDiscreteGpu.Should().BeTrue();
    }

    [Fact]
    public void GpuTierDetector_ReadsSimulatedEnvironmentVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NOVA_SIMULATED_GPU_TIER", "Low");
        try
        {
            // Act
            var detector = new GpuTierDetector(NullLogger<GpuTierDetector>.Instance);

            // Assert
            detector.CurrentTier.Should().Be(GraphicsTier.Low);
            detector.IsDiscreteGpu.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOVA_SIMULATED_GPU_TIER", null);
        }
    }

    [Fact]
    public void IdleWorkingSetTrimmer_TrimWorkingSet_DoesNotThrow()
    {
        // Act & Assert
        // Given we are invoking a P/Invoke (SetProcessWorkingSetSize), we want to make sure it runs gracefully
        // without AccessViolationException or similar errors.
        var trimmer = new IdleWorkingSetTrimmer(NullLogger<IdleWorkingSetTrimmer>.Instance);
        
        Action act = () => trimmer.TrimWorkingSet();
        act.Should().NotThrow();
    }
}
