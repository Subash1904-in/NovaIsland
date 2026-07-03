// Nova Island Watchdog — Independent crash recovery process.
// Communicates with the main app via named pipes (see SRS §7).
// Intentionally minimal: no WinRT, no heavy frameworks.

using Serilog;

namespace NovaIsland.Watchdog;

/// <summary>
/// Watchdog entry point. Monitors the main Nova Island process
/// via named-pipe heartbeat and restarts it on detected hang/crash.
/// </summary>
/// <remarks>
/// CLI arguments:
///   --target-exe &lt;path&gt;  : Path to the main app executable (required).
///   --pipe-name &lt;name&gt;   : Named pipe identifier (default: NovaIsland_Heartbeat).
///   --pid &lt;pid&gt;          : Attach to an already-running process instead of launching a new one.
/// </remarks>
public static class Program
{
    private const string DefaultPipeName = "NovaIsland_Heartbeat";

    /// <summary>
    /// Watchdog main entry point.
    /// </summary>
    /// <param name="args">Command-line arguments (see class remarks).</param>
    /// <returns>Exit code (0 = clean shutdown, 1 = error).</returns>
    public static async Task<int> Main(string[] args)
    {
        // Configure Serilog — minimal: console + rolling file.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine("logs", "watchdog-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                shared: true)
            .CreateLogger();

        try
        {
            Log.Information("[Watchdog] Nova Island Watchdog starting...");

            var (targetExe, pipeName, attachPid) = ParseArgs(args);

            if (string.IsNullOrEmpty(targetExe) && attachPid < 0)
            {
                Log.Error("[Watchdog] --target-exe is required (or --pid to attach to existing process)");
                return 1;
            }

            targetExe ??= string.Empty;

            using var cts = new CancellationTokenSource();

            // Handle Ctrl+C / SIGTERM gracefully.
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Log.Information("[Watchdog] Shutdown signal received");
                cts.Cancel();
            };

            using var processManager = new ProcessManager(targetExe, Log.Logger);

            // Either attach to an existing process or start a new one.
            if (attachPid >= 0)
            {
                if (!processManager.AttachToProcess(attachPid))
                {
                    Log.Error("[Watchdog] Could not attach to PID {Pid}", attachPid);
                    return 1;
                }
            }
            else
            {
                processManager.StartProcess();
            }

            using var heartbeatServer = new HeartbeatServer(
                pipeName,
                processManager,
                Log.Logger);

            await heartbeatServer.RunAsync(cts.Token).ConfigureAwait(false);

            Log.Information("[Watchdog] Watchdog shut down cleanly");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Fatal(ex, "[Watchdog] Watchdog terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Parses command-line arguments. Simple key-value pairs.
    /// </summary>
    private static (string? TargetExe, string PipeName, int AttachPid) ParseArgs(string[] args)
    {
        string? targetExe = null;
        var pipeName = DefaultPipeName;
        var attachPid = -1;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--target-exe":
                    targetExe = args[++i];
                    break;
                case "--pipe-name":
                    pipeName = args[++i];
                    break;
                case "--pid":
                    if (int.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out var pid))
                    {
                        attachPid = pid;
                    }

                    break;
            }
        }

        return (targetExe, pipeName, attachPid);
    }
}
