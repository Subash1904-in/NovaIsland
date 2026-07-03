using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NovaIsland.Infrastructure.Stability;

public class IdleWorkingSetTrimmer : BackgroundService
{
    private readonly ILogger<IdleWorkingSetTrimmer> _logger;
    private readonly TimeSpan _idleThreshold = TimeSpan.FromMinutes(2); // Example threshold
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    // Mock indicator for idleness (in a real app, track input events or system idle time)
    public static DateTime LastInteractionTime { get; set; } = DateTime.UtcNow;

    public IdleWorkingSetTrimmer(ILogger<IdleWorkingSetTrimmer> logger)
    {
        _logger = logger;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IdleWorkingSetTrimmer started.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);

                var timeSinceLastInteraction = DateTime.UtcNow - LastInteractionTime;
                if (timeSinceLastInteraction > _idleThreshold)
                {
                    TrimWorkingSet();
                    // Prevent continuous trimming, reset the timer logically by setting to now
                    // minus threshold plus 5 seconds (so it trims again in 5 mins if no action).
                    // For test simplicity, we just log and wait for next interval.
                }
            }
            catch (TaskCanceledException)
            {
                break; // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IdleWorkingSetTrimmer loop");
            }
        }
    }

    public void TrimWorkingSet()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            _logger.LogInformation("Trimming working set. Current: {CurrentMB} MB", process.WorkingSet64 / 1024 / 1024);
            
            // Passing -1, -1 asks the OS to swap as much of the working set to disk as possible
            nuint allOnes = unchecked((nuint)(long)-1);
            SetProcessWorkingSetSize(process.Handle, allOnes, allOnes);
            
            // Refresh to see if it dropped
            process.Refresh();
            _logger.LogInformation("Working set trimmed. New: {NewMB} MB", process.WorkingSet64 / 1024 / 1024);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trim working set memory.");
        }
    }
}
