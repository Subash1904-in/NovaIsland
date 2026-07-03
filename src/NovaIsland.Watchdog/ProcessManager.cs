using System.Diagnostics;
using Serilog;

namespace NovaIsland.Watchdog;

/// <summary>
/// Manages the lifecycle of the main Nova Island application process.
/// Supports starting, killing, and restarting the target process with
/// crash-reason logging via Serilog.
/// </summary>
public sealed class ProcessManager : IDisposable
{
    private readonly string _targetExePath;
    private readonly ILogger _logger;
    private Process? _currentProcess;
    private readonly object _processLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether the process manager is currently tracking a live process.
    /// </summary>
    public bool HasTargetProcess
    {
        get
        {
            lock (_processLock)
            {
                return _currentProcess is not null && !_currentProcess.HasExited;
            }
        }
    }

    /// <summary>
    /// Gets the PID of the current target process, or -1 if no process is tracked.
    /// </summary>
    public int CurrentPid
    {
        get
        {
            lock (_processLock)
            {
                return _currentProcess is not null && !_currentProcess.HasExited
                    ? _currentProcess.Id
                    : -1;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessManager"/> class.
    /// </summary>
    /// <param name="targetExePath">Path to the main app executable.</param>
    /// <param name="logger">Serilog logger instance.</param>
    public ProcessManager(string targetExePath, ILogger logger)
    {
        _targetExePath = targetExePath;
        _logger = logger;
    }

    /// <summary>
    /// Starts the target process and begins tracking it.
    /// </summary>
    /// <returns>The PID of the started process.</returns>
    /// <exception cref="InvalidOperationException">If the process could not be started.</exception>
    public int StartProcess()
    {
        lock (_processLock)
        {
            _logger.Information("[Watchdog] Starting target process: {ExePath}", _targetExePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = _targetExePath,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            _currentProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Failed to start process: {_targetExePath}");

            _logger.Information("[Watchdog] Target process started with PID {Pid}", _currentProcess.Id);
            return _currentProcess.Id;
        }
    }

    /// <summary>
    /// Forcefully kills the currently tracked process, if it is still running.
    /// </summary>
    public void KillProcess()
    {
        lock (_processLock)
        {
            if (_currentProcess is null || _currentProcess.HasExited)
            {
                _logger.Information("[Watchdog] No active process to kill");
                return;
            }

            try
            {
                var pid = _currentProcess.Id;
                _logger.Warning("[Watchdog] Killing process PID {Pid}", pid);
                _currentProcess.Kill(entireProcessTree: true);
                _currentProcess.WaitForExit(TimeSpan.FromSeconds(5));
                _logger.Information("[Watchdog] Process PID {Pid} killed", pid);
            }
            catch (InvalidOperationException)
            {
                _logger.Information("[Watchdog] Process already exited during kill");
            }
            finally
            {
                _currentProcess.Dispose();
                _currentProcess = null;
            }
        }
    }

    /// <summary>
    /// Kills the currently tracked process (if running) and starts a new instance.
    /// Logs the restart reason for crash diagnostics.
    /// </summary>
    /// <param name="reason">The reason for the restart, logged to Serilog.</param>
    /// <returns>The PID of the newly started process.</returns>
    public int RestartProcess(string reason)
    {
        _logger.Error(
            "[Watchdog] Restart triggered — Reason: {Reason}",
            reason);

        KillProcess();
        return StartProcess();
    }

    /// <summary>
    /// Attaches to an already-running process by PID instead of launching a new one.
    /// Used when the watchdog starts after the main app is already running.
    /// </summary>
    /// <param name="pid">The PID to track.</param>
    /// <returns><c>true</c> if the process was found and attached; <c>false</c> otherwise.</returns>
    public bool AttachToProcess(int pid)
    {
        lock (_processLock)
        {
            try
            {
                _currentProcess = Process.GetProcessById(pid);
                _logger.Information("[Watchdog] Attached to existing process PID {Pid}", pid);
                return true;
            }
            catch (ArgumentException)
            {
                _logger.Warning("[Watchdog] Process PID {Pid} not found", pid);
                return false;
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

        lock (_processLock)
        {
            _currentProcess?.Dispose();
            _currentProcess = null;
        }
    }
}
