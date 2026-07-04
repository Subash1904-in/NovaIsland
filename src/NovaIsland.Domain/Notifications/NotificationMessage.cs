namespace NovaIsland.Domain.Notifications;

public record NotificationMessage(
    string Id,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    string? SourceAppPath = null
);
