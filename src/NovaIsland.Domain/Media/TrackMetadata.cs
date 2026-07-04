namespace NovaIsland.Domain.Media;

/// <summary>
/// Represents the playback state of the media session.
/// </summary>
public enum PlaybackStatus
{
    Unknown = 0,
    Closed = 1,
    Opened = 2,
    Changing = 3,
    Stopped = 4,
    Playing = 5,
    Paused = 6
}

/// <summary>
/// Represents the metadata of the currently playing track.
/// </summary>
public record TrackMetadata(
    string Title,
    string Artist,
    PlaybackStatus Status,
    TimeSpan Position,
    TimeSpan EndTime,
    DateTimeOffset LastUpdatedTime,
    string? SourceAppUserModelId = null
);
