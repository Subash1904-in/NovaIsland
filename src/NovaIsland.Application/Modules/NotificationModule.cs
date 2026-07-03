using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Notifications;

namespace NovaIsland.Application.Modules;

public class NotificationModule : INovaModule
{
    private readonly INotificationService _notificationService;
    private readonly IFocusAssistProvider _focusAssistProvider;
    private readonly ILogger<NotificationModule> _logger;

    public string ModuleName => "Notifications";

    public NotificationModule(
        INotificationService notificationService,
        IFocusAssistProvider focusAssistProvider,
        ILogger<NotificationModule> logger)
    {
        _notificationService = notificationService;
        _focusAssistProvider = focusAssistProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notification Module started");

        await _notificationService.InitializeAsync();
        _notificationService.NotificationReceived += OnNotificationReceived;

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            _notificationService.NotificationReceived -= OnNotificationReceived;
            _logger.LogInformation("Notification Module stopped");
        }
    }

    private void OnNotificationReceived(object? sender, NotificationMessage message)
    {
        if (_focusAssistProvider.IsFocusAssistActive())
        {
            _logger.LogInformation("Notification received but Focus Assist is active. Suppressing alert.");
            return;
        }

        _logger.LogInformation("Notification received: {Title} from {App}. Triggering alert.", message.Title, message.AppName);

        // We will invoke the IslandShellService to show the alert here.
        // We do this via an event or injected service callback in the real integration.
        OnAlertTriggered?.Invoke(this, message);
    }

    // A lightweight way to decouple the UI shell from the application module
    public event EventHandler<NotificationMessage>? OnAlertTriggered;
}
