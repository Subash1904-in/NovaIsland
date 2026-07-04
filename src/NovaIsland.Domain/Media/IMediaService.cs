namespace NovaIsland.Domain.Media;

/// <summary>
/// Service interface to get current media playback information
/// and subscribe to track changes.
/// </summary>
public interface IMediaService
{
    TrackMetadata? CurrentTrack { get; }
    
    Task PlayPauseAsync();
    Task PreviousAsync();
    Task NextAsync();
    
    event EventHandler<TrackMetadata>? TrackChanged;
}
