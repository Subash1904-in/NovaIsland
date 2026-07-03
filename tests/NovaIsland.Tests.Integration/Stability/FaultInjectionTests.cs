using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.Application.Stability;
using NovaIsland.Infrastructure.Stability;
using Xunit;

namespace NovaIsland.Tests.Integration.Stability;

/// <summary>
/// Fault-injection tests proving module isolation and supervision boundary correctness.
/// These tests verify SRS §7: "zero full-app crashes from module/plugin faults."
/// </summary>
public sealed class FaultInjectionTests
{
    /// <summary>
    /// Verifies that a faulty module crashing does NOT bring down the host
    /// or affect healthy modules. The faulty module is eventually marked degraded.
    /// </summary>
    [Fact]
    public async Task ModuleFault_DoesNotCrashShell_HealthyModuleKeepsRunning()
    {
        // Arrange: set up a host with one healthy and one faulty module.
        var healthReporter = new ModuleHealthReporter();
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            ModuleMaxRetries = 3,
            CrashLoopThreshold = 10, // High threshold so rollback doesn't fire.
            CrashLoopWindowMinutes = 1,
        });
        var crashLoopDetector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        var faultyModule = new FaultyModule("Faulty", throwAfterExecutions: 0);
        var healthyModule = new HealthyModule("Healthy");

        var faultySupervisor = new SupervisedModuleService(
            faultyModule,
            healthReporter,
            crashLoopDetector,
            options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        var healthySupervisor = new SupervisedModuleService(
            healthyModule,
            healthReporter,
            crashLoopDetector,
            options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act: start both modules.
        await faultySupervisor.StartAsync(cts.Token);
        await healthySupervisor.StartAsync(cts.Token);

        // Wait for the faulty module to exhaust retries.
        // 3 retries with exponential backoff: 500ms + 1000ms + 2000ms = ~3.5s max.
        await Task.Delay(5000);

        // Assert: faulty module is degraded.
        healthReporter.GetHealth("Faulty").Should().Be(ModuleHealth.Degraded,
            "faulty module should be marked degraded after exhausting retries");

        // Assert: healthy module is still running.
        healthReporter.GetHealth("Healthy").Should().Be(ModuleHealth.Running,
            "healthy module must keep running despite other module's crashes");

        // Assert: healthy module has been ticking.
        healthyModule.TickCount.Should().BeGreaterThan(0,
            "healthy module should have processed ticks while faulty module crashed");

        // Cleanup.
        await cts.CancelAsync();
        await faultySupervisor.StopAsync(CancellationToken.None);
        await healthySupervisor.StopAsync(CancellationToken.None);

        faultySupervisor.Dispose();
        healthySupervisor.Dispose();
    }

    /// <summary>
    /// Verifies that the host never crashes even when a module throws repeatedly.
    /// The host must remain responsive (verifiable by querying health reporter).
    /// </summary>
    [Fact]
    public async Task RepeatedModuleCrashes_HostRemainsAlive()
    {
        // Arrange.
        var healthReporter = new ModuleHealthReporter();
        var stubRollback = new StubUpdateRollback(
            new LoggerFactory().CreateLogger<StubUpdateRollback>());
        var options = Options.Create(new StabilityOptions
        {
            ModuleMaxRetries = 2,
            CrashLoopThreshold = 10,
            CrashLoopWindowMinutes = 1,
        });
        var crashLoopDetector = new CrashLoopDetector(
            stubRollback,
            options,
            new LoggerFactory().CreateLogger<CrashLoopDetector>());

        var faultyModule = new FaultyModule("CrashTest", throwAfterExecutions: 0);

        var supervisor = new SupervisedModuleService(
            faultyModule,
            healthReporter,
            crashLoopDetector,
            options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act.
        await supervisor.StartAsync(cts.Token);
        await Task.Delay(3000); // Wait for retries + backoff.

        // Assert: module is degraded, not crashed.
        healthReporter.GetHealth("CrashTest").Should().Be(ModuleHealth.Degraded);

        // Assert: the test itself hasn't crashed (= the host is alive).
        // Health reporter is still queryable — proves the shell is responsive.
        var allHealth = healthReporter.GetAllHealth();
        allHealth.Should().ContainKey("CrashTest");

        // Cleanup.
        await cts.CancelAsync();
        await supervisor.StopAsync(CancellationToken.None);
        supervisor.Dispose();
    }

    /// <summary>
    /// Verifies that module crash count is correctly tracked and the
    /// faulty module is retried the exact number of max retries.
    /// </summary>
    [Fact]
    public async Task FaultyModule_RetriedExactlyMaxRetries_ThenDegraded()
    {
        // Arrange.
        const int maxRetries = 3;
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

        var faultyModule = new FaultyModule("RetryTest", throwAfterExecutions: 0);

        var supervisor = new SupervisedModuleService(
            faultyModule,
            healthReporter,
            crashLoopDetector,
            options,
            new LoggerFactory().CreateLogger<SupervisedModuleService>());

        using var cts = new CancellationTokenSource();

        // Act.
        await supervisor.StartAsync(cts.Token);
        await Task.Delay(6000); // Generous wait for exponential backoff.

        // Assert: module was called exactly maxRetries times (each call throws).
        faultyModule.ExecutionCount.Should().Be(maxRetries,
            $"module should be called exactly {maxRetries} times before being degraded");

        healthReporter.GetHealth("RetryTest").Should().Be(ModuleHealth.Degraded);

        // Cleanup.
        await cts.CancelAsync();
        await supervisor.StopAsync(CancellationToken.None);
        supervisor.Dispose();
    }
}
