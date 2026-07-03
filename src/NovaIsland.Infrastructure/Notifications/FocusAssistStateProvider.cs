using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NovaIsland.Domain.Notifications;

namespace NovaIsland.Infrastructure.Notifications;

public class FocusAssistStateProvider : IFocusAssistProvider
{
    private readonly ILogger<FocusAssistStateProvider> _logger;

    public FocusAssistStateProvider(ILogger<FocusAssistStateProvider> logger)
    {
        _logger = logger;
    }

    public bool IsFocusAssistActive()
    {
        try
        {
            // Focus Assist state is often stored in QuietHours registry keys in Windows 10/11
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
            if (key != null)
            {
                var val = key.GetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED");
                if (val is int intVal)
                {
                    // 0 = Toasts Disabled (Focus Assist ON)
                    // 1 = Toasts Enabled (Focus Assist OFF)
                    return intVal == 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Focus Assist state from registry. Defaulting to false.");
        }
        
        return false;
    }
}
