using System.IO.Pipes;
using Serilog;

namespace NovaIsland.Watchdog;

/// <summary>
/// Named-pipe server that receives heartbeat messages from the main Nova Island process.
/// Runs a parallel timer that counts missed heartbeats. If 4 consecutive heartbeats are
/// missed (≈ 2 s total), the watchdog triggers a restart via <see cref="ProcessManager"/>.
/// </summary>
/// <remarks>
/// Protocol: The main app connects to the named pipe and writes <c>PING\n</c> every 500 ms.
/// The watchdog reads these messages and resets the miss counter on each received heartbeat.
/// </remarks>
public sealed class HeartbeatServer : IDisposable
{
    private readonly string _pipeName;
    private readonly int _missThreshold;
    private readonly int _checkIntervalMs;
    private readonly ProcessManager _processManager;
    private readonly ILogger _logger;

    private int _missedHeartbeats;
    private volatile bool _connected;
    private volatile bool _everConnected;
    private volatile bool _disposed;

    /// <summary>
    /// Raised when a restart is triggered due to missed heartbeats.
    /// Exposed for testing.
    /// </summary>
    public event EventHandler<string>? RestartTriggered;

    /// <summary>
    /// Gets the current count of consecutive missed heartbeats.
    /// </summary>
    public int MissedHeartbeats => _missedHeartbeats;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatServer"/> class.
    /// </summary>
    /// <param name="pipeName">The named pipe identifier.</param>
    /// <param name="processManager">Manages the target process lifecycle.</param>
    /// <param name="logger">Serilog logger instance.</param>
    /// <param name="missThreshold">Number of consecutive misses before restart (default: 4).</param>
    /// <param name="checkIntervalMs">Heartbeat check interval in milliseconds (default: 500).</param>
    public HeartbeatServer(
        string pipeName,
        ProcessManager processManager,
        ILogger logger,
        int missThreshold = 4,
        int checkIntervalMs = 500)
    {
        _pipeName = pipeName;
        _processManager = processManager;
        _logger = logger;
        _missThreshold = missThreshold;
        _checkIntervalMs = checkIntervalMs;
    }

    /// <summary>
    /// Runs the heartbeat server loop: listens for pipe connections, reads heartbeats,
    /// and triggers restarts on missed heartbeats. Runs until cancellation.
    /// </summary>
    /// <param name="cancellationToken">Token to stop the server.</param>
    /// <returns>A task that completes when the server shuts down.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.Information("[Watchdog] HeartbeatServer starting on pipe: {PipeName}", _pipeName);

        // Start the miss-detection timer on a background task.
        var missDetectionTask = RunMissDetectionAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ListenForHeartbeatsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[Watchdog] Pipe connection error, will retry");
                _connected = false;

                // Brief pause before re-creating the pipe server.
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        await missDetectionTask.ConfigureAwait(false);
        _logger.Information("[Watchdog] HeartbeatServer stopped");
    }

    /// <summary>
    /// Listens on a single pipe connection for heartbeat messages.
    /// Returns when the client disconnects.
    /// </summary>
    private async Task ListenForHeartbeatsAsync(CancellationToken cancellationToken)
    {
        await using var pipeServer = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            System.IO.Pipes.PipeOptions.Asynchronous);

        _logger.Information("[Watchdog] Waiting for heartbeat client connection...");
        await pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        _connected = true;
        _everConnected = true;
        Interlocked.Exchange(ref _missedHeartbeats, 0);
        _logger.Information("[Watchdog] Heartbeat client connected");

        using var reader = new StreamReader(pipeServer);

        while (!cancellationToken.IsCancellationRequested && pipeServer.IsConnected)
        {
            try
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    // Client disconnected (EOF).
                    _logger.Warning("[Watchdog] Heartbeat client disconnected (EOF)");
                    break;
                }

                if (string.Equals(line, "PING", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Exchange(ref _missedHeartbeats, 0);
                }
            }
            catch (IOException)
            {
                _logger.Warning("[Watchdog] Pipe broken — client disconnected");
                break;
            }
        }

        _connected = false;
    }

    /// <summary>
    /// Background timer that increments the miss counter every <see cref="_checkIntervalMs"/>
    /// and triggers a restart when the threshold is reached.
    /// </summary>
    private async Task RunMissDetectionAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_checkIntervalMs));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Only count misses after we've had at least one connection.
            // After first-ever connection, any disconnect means the app died or hung.
            if (!_connected && _everConnected)
            {
                var currentMisses = Interlocked.Increment(ref _missedHeartbeats);

                if (currentMisses >= _missThreshold)
                {
                    var reason = $"Missed {currentMisses} consecutive heartbeats (threshold: {_missThreshold})";
                    _logger.Error("[Watchdog] {Reason} — triggering restart", reason);

                    Interlocked.Exchange(ref _missedHeartbeats, 0);
                    RestartTriggered?.Invoke(this, reason);
                    _processManager.RestartProcess(reason);
                }
            }
            else if (_connected)
            {
                // If connected but no heartbeat received, increment miss counter.
                var currentMisses = Interlocked.Increment(ref _missedHeartbeats);

                if (currentMisses >= _missThreshold)
                {
                    var reason = $"Main app unresponsive — {currentMisses} missed heartbeats while connected";
                    _logger.Error("[Watchdog] {Reason} — triggering restart", reason);

                    Interlocked.Exchange(ref _missedHeartbeats, 0);
                    _connected = false;
                    RestartTriggered?.Invoke(this, reason);
                    _processManager.RestartProcess(reason);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
