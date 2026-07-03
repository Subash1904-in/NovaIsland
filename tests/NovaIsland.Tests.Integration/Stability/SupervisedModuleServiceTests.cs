using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Application.Stability;
using NovaIsland.Infrastructure.Stability;
using Xunit;

namespace NovaIsland.Tests.Integration.Stability;

/// <summary>
/// Tests for the <see cref="SupervisedModuleService"/> supervision boundary.
/// Validates exponential backoff, max retries, and module isolation.
/// </summary>
public sealed class SupervisedModuleServiceTests
{
    /// <summary>
    /// Verifies that exponential backoff delays increase correctly:
    /// 500ms, 1s, 2s, 4s, 8s, 8s (capped).
    /// </summary>
    [Theory]
    [InlineData(1, 500)]
    [InlineData(2, 1000)]
    [InlineData(3, 2000)]
    [InlineData(4, 4000)]
    [InlineData(5, 8000)]
    [InlineData(6, 8000)] // Capped at MaxBackoffDelay.
    [InlineData(10, 8000)]
    public void ExponentialBackoff_IncreasesDelay(int restartCount, int expectedMs)
    {
        // Act.
        var backoff = SupervisedModuleService.CalculateBackoff(restartCount);

        // Assert.
        backoff.TotalMilliseconds.Should().Be(expectedMs);
    }

    /// <summary>
    /// After max retries, the module must be marked <see cref="ModuleHealth.Degraded"/>
    /// and no further restarts should occur.
    /// </summary>
    [Fact]
    public async Task MaxRetries_MarksModuleDegraded_NoMoreRestarts()
    {
        // Arrange.
        const int maxRetries = 2;
        var healthReporter = new ModuleHealthReporter();
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            ModuleMaxRetries = maxRetries,
            CrashLoopThreshold = 100,
            CrashLoopWindowMinutes = 1,
        });
        var crashLoopDetector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        var faultyModule = new FaultyModule("DegradedTest", throwAfterExecutions: 0);

        var supervisor = new SupervisedModuleService(
            faultyModule,
            healthReporter,
            crashLoopDetector,
            options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act.
        await supervisor.StartAsync(cts.Token);
        await Task.Delay(4000); // Wait for backoff: 500ms + 1000ms + buffer.

        // Assert.
        healthReporter.GetHealth("DegradedTest").Should().Be(ModuleHealth.Degraded);
        faultyModule.ExecutionCount.Should().Be(maxRetries,
            "module should stop being retried after max retries");

        // Cleanup.
        await cts.CancelAsync();
        await supervisor.StopAsync(CancellationToken.None);
        supervisor.Dispose();
    }

    /// <summary>
    /// Two modules run simultaneously — one crashes, the other must keep running.
    /// </summary>
    [Fact]
    public async Task SingleModuleCrash_DoesNotAffectOthers()
    {
        // Arrange.
        var healthReporter = new ModuleHealthReporter();
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            ModuleMaxRetries = 1,
            CrashLoopThreshold = 100,
            CrashLoopWindowMinutes = 1,
        });
        var crashLoopDetector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        var crasher = new FaultyModule("Crasher");
        var survivor = new HealthyModule("Survivor");

        var crasherSupervisor = new SupervisedModuleService(
            crasher, healthReporter, crashLoopDetector, options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());
        var survivorSupervisor = new SupervisedModuleService(
            survivor, healthReporter, crashLoopDetector, options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act.
        await crasherSupervisor.StartAsync(cts.Token);
        await survivorSupervisor.StartAsync(cts.Token);
        await Task.Delay(2000);

        // Assert.
        healthReporter.GetHealth("Crasher").Should().Be(ModuleHealth.Degraded);
        healthReporter.GetHealth("Survivor").Should().Be(ModuleHealth.Running);
        survivor.TickCount.Should().BeGreaterThan(0,
            "survivor module must continue ticking while crasher is degraded");

        // Cleanup.
        await cts.CancelAsync();
        await crasherSupervisor.StopAsync(CancellationToken.None);
        await survivorSupervisor.StopAsync(CancellationToken.None);
        crasherSupervisor.Dispose();
        survivorSupervisor.Dispose();
    }

    /// <summary>
    /// A module that completes normally (returns without throwing) should
    /// be marked as having completed without being retried.
    /// </summary>
    [Fact]
    public async Task NormalCompletion_NoRestart()
    {
        // Arrange.
        var healthReporter = new ModuleHealthReporter();
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            ModuleMaxRetries = 5,
            CrashLoopThreshold = 100,
            CrashLoopWindowMinutes = 1,
        });
        var crashLoopDetector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        // A module that doesn't throw and completes immediately.
        var successModule = new FaultyModule("Success", throwAfterExecutions: 100);

        var supervisor = new SupervisedModuleService(
            successModule, healthReporter, crashLoopDetector, options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act.
        await supervisor.StartAsync(cts.Token);
        await Task.Delay(500);

        // Assert: module ran once and completed, not degraded.
        successModule.ExecutionCount.Should().Be(1);
        healthReporter.GetHealth("Success").Should().NotBe(ModuleHealth.Degraded);

        // Cleanup.
        await cts.CancelAsync();
        await supervisor.StopAsync(CancellationToken.None);
        supervisor.Dispose();
    }
}
