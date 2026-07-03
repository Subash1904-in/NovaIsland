using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Background service that sends heartbeat messages to the watchdog process
/// via a named pipe. Reconnects automatically on broken pipe.
/// </summary>
public sealed class HeartbeatClientService : BackgroundService
{
    private readonly StabilityOptions _options;
    private readonly ILogger<HeartbeatClientService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatClientService"/> class.
    /// </summary>
    /// <param name="options">Stability configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public HeartbeatClientService(
        IOptions<StabilityOptions> options,
        ILogger<HeartbeatClientService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "HeartbeatClient starting — pipe: {PipeName}, interval: {IntervalMs}ms",
            _options.HeartbeatPipeName,
            _options.HeartbeatIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat pipe connection lost, reconnecting in 1s...");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("HeartbeatClient stopped");
    }

    /// <summary>
    /// Connects to the watchdog pipe and sends heartbeats until disconnected.
    /// </summary>
    private async Task SendHeartbeatsAsync(CancellationToken stoppingToken)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            _options.HeartbeatPipeName,
            PipeDirection.Out,
            System.IO.Pipes.PipeOptions.Asynchronous);

        // Wait up to 5s for the watchdog pipe to become available.
        await pipeClient.ConnectAsync(5000, stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("HeartbeatClient connected to watchdog pipe");

        await using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

        while (!stoppingToken.IsCancellationRequested && pipeClient.IsConnected)
        {
            await writer.WriteLineAsync("PING".AsMemory(), stoppingToken).ConfigureAwait(false);
            await Task.Delay(_options.HeartbeatIntervalMs, stoppingToken).ConfigureAwait(false);
        }
    }
}
