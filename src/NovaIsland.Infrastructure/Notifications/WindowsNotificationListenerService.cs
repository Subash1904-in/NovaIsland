using Microsoft.Extensions.Logging;
using NovaIsland.Domain.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Notifications;
using System.Runtime.InteropServices.WindowsRuntime;

namespace NovaIsland.Infrastructure.Notifications;

public class WindowsNotificationListenerService : INotificationService
{
    private readonly ILogger<WindowsNotificationListenerService> _logger;
    private UserNotificationListener? _listener;

    public event EventHandler<NotificationMessage>? NotificationReceived;

    public WindowsNotificationListenerService(ILogger<WindowsNotificationListenerService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var accessStatus = await _listener.RequestAccessAsync();
            
            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                _logger.LogWarning("UserNotificationListener access was denied. Notifications will not be captured.");
                return;
            }

            _listener.NotificationChanged += OnNotificationChanged;
            _logger.LogInformation("WindowsNotificationListenerService initialized successfully.");
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80070490)
        {
            _logger.LogWarning("Windows notification listener is not supported in the current environment or the element was not found (0x80070490). Notifications will not be captured.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WindowsNotificationListenerService.");
        }
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        try
        {
            if (args.ChangeKind == UserNotificationChangedKind.Added)
            {
                var notification = _listener?.GetNotification(args.UserNotificationId);
                if (notification != null)
                {
                    string appName = notification.AppInfo?.DisplayInfo?.DisplayName ?? "Unknown App";
                    string title = "";
                    string body = "";

                    var bindings = notification.Notification.Visual.Bindings;
                    if (bindings != null && bindings.Count > 0)
                    {
                        var textElements = bindings[0].GetTextElements();
                        if (textElements.Count > 0)
                        {
                            title = textElements[0].Text;
                        }
                        if (textElements.Count > 1)
                        {
                            body = textElements[1].Text;
                        }
                    }

                    var msg = new NotificationMessage(
                        Id: notification.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        AppName: appName,
                        Title: title,
                        Body: body,
                        Timestamp: notification.CreationTime
                    );

                    NotificationReceived?.Invoke(this, msg);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification change.");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_listener != null)
        {
            _listener.NotificationChanged -= OnNotificationChanged;
            _listener = null;
        }
    }
}
