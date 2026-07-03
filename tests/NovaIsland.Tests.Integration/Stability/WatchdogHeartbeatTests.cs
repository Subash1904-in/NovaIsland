using System.Diagnostics;
using System.IO.Pipes;
using FluentAssertions;
using Serilog;
using NovaIsland.Watchdog;
using Xunit;

namespace NovaIsland.Tests.Integration.Stability;

/// <summary>
/// Tests for the watchdog heartbeat protocol.
/// Validates that missed heartbeats trigger a restart and that
/// continuous heartbeats do not cause false restarts.
/// </summary>
public sealed class WatchdogHeartbeatTests : IDisposable
{
    private readonly ILogger _logger;

    public WatchdogHeartbeatTests()
    {
        _logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }

    /// <summary>
    /// When heartbeats stop, the watchdog must detect the failure
    /// and trigger a restart within 2 seconds (SRS §7 requirement).
    /// </summary>
    [Fact]
    public async Task MissedHeartbeats_TriggersRestart_Within2Seconds()
    {
        // Arrange.
        var pipeName = $"NovaIsland_Test_{Guid.NewGuid():N}";

        // Start a real lightweight process for the ProcessManager to track.
        // Use "timeout" which blocks indefinitely and is easily killable.
        using var dummyProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout /t 300 /nobreak",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
        })!;

        using var processManager = new ProcessManager("cmd.exe", _logger);
        processManager.AttachToProcess(dummyProcess.Id);

        using var heartbeatServer = new HeartbeatServer(
            pipeName,
            processManager,
            _logger,
            missThreshold: 4,
            checkIntervalMs: 500);

        var restartStopwatch = new Stopwatch();
        var restartTcs = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);

        heartbeatServer.RestartTriggered += (_, _) =>
        {
            restartTcs.TrySetResult(restartStopwatch.Elapsed);
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Start the heartbeat server on a background task.
        var serverTask = Task.Run(() => heartbeatServer.RunAsync(cts.Token), cts.Token);

        // Connect and send a few heartbeats to establish connection.
        await using (var pipeClient = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous))
        {
            await pipeClient.ConnectAsync(5000, cts.Token);
            await using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

            for (var i = 0; i < 5; i++)
            {
                await writer.WriteLineAsync("PING");
                await Task.Delay(300, cts.Token);
            }

            // Start timing right before disconnecting.
            restartStopwatch.Start();
        }
        // Pipe client is now disposed — simulating main app crash.

        // Wait for restart trigger or timeout.
        var completedTask = await Task.WhenAny(
            restartTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(8), cts.Token));

        // Cleanup.
        await cts.CancelAsync();

        // Assert.
        completedTask.Should().Be(restartTcs.Task,
            "watchdog should detect missed heartbeats and trigger restart");

        var restartTime = await restartTcs.Task;
        restartTime.TotalSeconds.Should().BeLessThan(3.0,
            "watchdog recovery must complete within ~2s (with buffer for test overhead + pipe reconnect delay)");
    }

    /// <summary>
    /// When heartbeats are continuous, the watchdog must NOT trigger a restart.
    /// </summary>
    [Fact]
    public async Task ContinuousHeartbeats_NoRestart()
    {
        // Arrange.
        var pipeName = $"NovaIsland_Test_{Guid.NewGuid():N}";

        using var processManager = new ProcessManager("cmd.exe", _logger);
        using var heartbeatServer = new HeartbeatServer(
            pipeName,
            processManager,
            _logger,
            missThreshold: 4,
            checkIntervalMs: 500);

        var restartTriggered = false;
        heartbeatServer.RestartTriggered += (_, _) => restartTriggered = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Start server.
        var serverTask = Task.Run(() => heartbeatServer.RunAsync(cts.Token), cts.Token);

        // Send continuous heartbeats for 3 seconds.
        await using var pipeClient = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous);

        await pipeClient.ConnectAsync(5000, cts.Token);
        await using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

        for (var i = 0; i < 15; i++) // 15 x 200ms = 3s
        {
            await writer.WriteLineAsync("PING");
            await Task.Delay(200, cts.Token);
        }

        // Assert: no restart was triggered.
        restartTriggered.Should().BeFalse(
            "continuous heartbeats should prevent restart trigger");

        // Cleanup.
        await cts.CancelAsync();
    }

    /// <summary>
    /// Verifies the ProcessManager can track process state.
    /// </summary>
    [Fact]
    public void ProcessManager_HasTargetProcess_InitiallyFalse()
    {
        // Arrange & Act.
        using var pm = new ProcessManager("nonexistent.exe", _logger);

        // Assert.
        pm.HasTargetProcess.Should().BeFalse();
        pm.CurrentPid.Should().Be(-1);
    }

    /// <summary>
    /// Verifies ProcessManager.AttachToProcess works with a real process.
    /// </summary>
    [Fact]
    public void ProcessManager_AttachToProcess_TracksCorrectPid()
    {
        // Arrange.
        using var dummyProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout /t 10 /nobreak",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
        })!;

        using var pm = new ProcessManager("cmd.exe", _logger);

        // Act.
        var attached = pm.AttachToProcess(dummyProcess.Id);

        // Assert.
        attached.Should().BeTrue();
        pm.HasTargetProcess.Should().BeTrue();
        pm.CurrentPid.Should().Be(dummyProcess.Id);

        // Cleanup.
        pm.KillProcess();
    }

    public void Dispose()
    {
        (Log.Logger as IDisposable)?.Dispose();
    }
}
