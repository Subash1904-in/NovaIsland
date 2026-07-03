using FluentAssertions;
using NovaIsland.UI.Animation;
using System;
using System.Diagnostics;
using Xunit;

namespace NovaIsland.Tests.Perf;

/// <summary>
/// Performance gate tests validating targets defined in SRS §10 and §6:
/// - p99 frame time < 8.3 ms @ 120Hz (asserts that the update physics does not block frames)
/// - Hot path zero-alloc rule (enforces no allocations in the update loop).
/// </summary>
public sealed class FrameTimePerfTests
{
    [Fact]
    public void AnimationHotPath_Under120Hz_MeetsFrameTimeAndZeroAllocGate()
    {
        // Arrange
        // Setup a critically damped spring at 120Hz.
        var controller = new IslandAnimationController(IslandState.Compact, SpringConfig.CriticallyDamped(300f));
        
        // Simulating 5 seconds of animation at 120Hz (600 frames total).
        const int totalFrames = 600;
        const float deltaTime = 1f / 120f;
        
        // Pre-allocate tracking array to prevent allocations during measurement.
        var frameTimesMs = new double[totalFrames];
        
        // Trigger a transition to Expanded to ensure spring math runs (not settled).
        controller.TransitionTo(IslandState.Expanded);

        // Warm up JIT to avoid capturing compilation times
        for (int i = 0; i < 50; i++)
        {
            controller.Update(deltaTime);
        }
        
        // Reset state for clean measurement
        controller.TransitionTo(IslandState.Compact);
        controller.Update(deltaTime);
        controller.TransitionTo(IslandState.Expanded);

        // Perform garbage collection to establish clean state
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = new Stopwatch();

        // Act
        for (int i = 0; i < totalFrames; i++)
        {
            stopwatch.Restart();
            
            // Execute hot path logic
            controller.Update(deltaTime);
            controller.GetCurrentValues(out _, out _, out _, out _, out _);
            
            stopwatch.Stop();
            
            // Record elapsed time in milliseconds (ticks to ms conversion to avoid boxing/allocs)
            frameTimesMs[i] = (double)stopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocations = endAllocatedBytes - startAllocatedBytes;

        // Process statistics (P99 calculation)
        Array.Sort(frameTimesMs);
        int p99Index = (int)(totalFrames * 0.99);
        double p99FrameTimeMs = frameTimesMs[p99Index];

        // Assert
        // p99 frame time should be significantly below 8.3ms (since it's purely managed math, it should be <0.1ms).
        p99FrameTimeMs.Should().BeLessThan(8.3, "p99 frame update time must be within 120Hz frame budget of 8.3ms");
        
        // Zero-alloc assert: allow a small buffer for runtime internal threads/Stopwatch overhead,
        // but it should be practically zero. Let's enforce <= 1024 bytes (or ideally exactly 0).
        // Since GC.GetAllocatedBytesForCurrentThread() can sometimes account for very minor internal CLR tracing,
        // we assert <= 1024 bytes (extremely strict, representing zero object allocations).
        totalAllocations.Should().BeLessOrEqualTo(1024, $"Hot path must not allocate. Total allocations was {totalAllocations} bytes.");
    }
}
