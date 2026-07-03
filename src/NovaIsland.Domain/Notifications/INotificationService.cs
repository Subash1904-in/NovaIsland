namespace NovaIsland.Domain.Notifications;

public interface INotificationService : IDisposable
{
    event EventHandler<NotificationMessage>? NotificationReceived;
    Task InitializeAsync();
}
