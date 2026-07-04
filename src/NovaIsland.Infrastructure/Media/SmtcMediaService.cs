using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NovaIsland.Domain.Media;
using Windows.Media.Control;

namespace NovaIsland.Infrastructure.Media;

public class SmtcMediaService : IMediaService, IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly ILogger<SmtcMediaService> _logger;
    private System.Threading.Timer? _progressTimer;
    private bool _isDisposed;

    public TrackMetadata? CurrentTrack { get; private set; }

    public event EventHandler<TrackMetadata>? TrackChanged;
    public event EventHandler<double>? ProgressChanged;

    public SmtcMediaService(ILogger<SmtcMediaService> logger)
    {
        _logger = logger;
        _progressTimer = new System.Threading.Timer(OnProgressTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SMTC Media Service");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession(sender.GetCurrentSession());
    }

    private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _currentSession = session;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }

        _ = RefreshTrackInfoAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshTrackInfoAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshTrackInfoAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = RefreshTrackInfoAsync();
    }

    private async Task RefreshTrackInfoAsync()
    {
        if (_currentSession == null)
        {
            CurrentTrack = null;
            return;
        }

        try
        {
            var properties = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var timeline = _currentSession.GetTimelineProperties();

            PlaybackStatus status = playbackInfo?.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => PlaybackStatus.Closed,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => PlaybackStatus.Opened,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackStatus.Changing,
                _ => PlaybackStatus.Unknown
            };

            var newTrack = new TrackMetadata(
                Title: properties?.Title ?? "Unknown Title",
                Artist: properties?.Artist ?? "Unknown Artist",
                Status: status,
                Position: timeline?.Position ?? TimeSpan.Zero,
                EndTime: timeline?.EndTime ?? TimeSpan.Zero,
                LastUpdatedTime: DateTimeOffset.UtcNow,
                SourceAppUserModelId: _currentSession.SourceAppUserModelId
            );

            CurrentTrack = newTrack;
            TrackChanged?.Invoke(this, newTrack);
            
            if (status == PlaybackStatus.Playing)
            {
                _progressTimer?.Change(0, 500);
            }
            else
            {
                _progressTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing SMTC track info");
        }
    }

    private void OnProgressTimerTick(object? state)
    {
        var track = CurrentTrack;
        if (track != null && track.Status == PlaybackStatus.Playing && track.EndTime > TimeSpan.Zero)
        {
            var elapsed = DateTimeOffset.UtcNow - track.LastUpdatedTime;
            var currentPosition = track.Position + elapsed;
            if (currentPosition > track.EndTime) currentPosition = track.EndTime;
            double progress = currentPosition.TotalSeconds / track.EndTime.TotalSeconds;
            ProgressChanged?.Invoke(this, progress);
        }
    }

    public async Task PlayPauseAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TryTogglePlayPauseAsync();
        }
    }

    public async Task PreviousAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TrySkipPreviousAsync();
        }
    }

    public async Task NextAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TrySkipNextAsync();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed) return;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        if (_sessionManager != null)
        {
            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        _progressTimer?.Dispose();
        _isDisposed = true;
    }
}
