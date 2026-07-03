using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NovaIsland.UI.Animation;
using NovaIsland.UI.Shell;

namespace NovaIsland.UI.FramePacing;

/// <summary>
/// Frame-pacing service that drives the island animation loop at the display's
/// refresh rate. Uses <see cref="Stopwatch"/> for delta-time computation
/// (no allocation) and pauses when animation is settled (zero CPU at idle).
/// </summary>
/// <remarks>
/// <para>
/// The timer is based on <see cref="System.Threading.PeriodicTimer"/> when running
/// headless, or a <c>DispatcherQueueTimer</c> when attached to a composition thread.
/// This implementation uses a standalone high-resolution timer suitable for both modes.
/// </para>
/// <para>
/// <b>Zero-allocation hot path</b>: <see cref="OnFrame"/> uses only stack locals,
/// pre-allocated Stopwatch, and ref-based animator calls. No LINQ, no boxing, no closures.
/// </para>
/// </remarks>
internal sealed class FramePacingService : IDisposable
{
    private readonly ILogger _logger;
    private readonly IIslandAnimator _animator;
    private readonly IslandVisualTree _visualTree;
    private readonly IslandWindow _window;
    private readonly DisplayRefreshDetector _refreshDetector;
    private readonly NovaIsland.Domain.Media.IMediaService _mediaService;

    // High-resolution timing via Stopwatch (no allocation per frame).
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;

    // Frame statistics.
    private float _lastFrameTimeMs;
    private bool _isRunning;
    private System.Threading.Timer? _frameTimer;

    /// <summary>
    /// Gets the duration of the most recent frame in milliseconds.
    /// Used for perf monitoring and the p99 frame-time gate.
    /// </summary>
    internal float LastFrameTimeMs => _lastFrameTimeMs;

    /// <summary>
    /// Gets or sets whether the frame timer is actively ticking.
    /// Set to false when animation is settled to achieve zero CPU at idle.
    /// </summary>
    internal bool IsRunning => _isRunning;

    /// <summary>
    /// Initializes a new <see cref="FramePacingService"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="animator">The animation controller to drive.</param>
    /// <param name="visualTree">The visual tree to update with animated values.</param>
    /// <param name="window">The island window for repositioning.</param>
    /// <param name="refreshDetector">The display refresh rate detector.</param>
    internal FramePacingService(
        ILogger logger,
        IIslandAnimator animator,
        IslandVisualTree visualTree,
        IslandWindow window,
        DisplayRefreshDetector refreshDetector,
        NovaIsland.Domain.Media.IMediaService mediaService)
    {
        _logger = logger;
        _animator = animator;
        _visualTree = visualTree;
        _window = window;
        _refreshDetector = refreshDetector;
        _mediaService = mediaService;
    }

    /// <summary>
    /// Starts the frame timer at the detected refresh rate.
    /// </summary>
    internal void Start()
    {
        if (_isRunning) return;

        _stopwatch.Restart();
        _lastFrameTicks = _stopwatch.ElapsedTicks;
        _isRunning = true;

        int intervalMs = (int)MathF.Max(1f, 1000f / _refreshDetector.RefreshRateHz);
        _frameTimer = new System.Threading.Timer(
            static state => ((FramePacingService)state!).OnFrame(),
            this,
            0,
            intervalMs);

        _logger.LogDebug("Frame pacing started: {Hz} Hz, interval {Interval} ms",
            _refreshDetector.RefreshRateHz, intervalMs);
    }

    /// <summary>
    /// Stops the frame timer.
    /// </summary>
    internal void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _frameTimer?.Dispose();
        _frameTimer = null;
        _stopwatch.Stop();

        _logger.LogDebug("Frame pacing stopped");
    }

    /// <summary>
    /// Resumes the frame timer when a new animation is triggered.
    /// Call this when <see cref="IIslandAnimator.TransitionTo"/> is invoked.
    /// </summary>
    internal void Resume()
    {
        if (_isRunning) return;
        Start();
    }

    /// <summary>
    /// Called once per frame. Computes delta-time, advances the animator,
    /// and applies results to the visual tree.
    /// </summary>
    /// <remarks>
    /// ZERO-ALLOC: Uses Stopwatch ticks (long) and float math only.
    /// No LINQ, no boxing, no closures, no string operations.
    /// </remarks>
    private void OnFrame()
    {
        long currentTicks = _stopwatch.ElapsedTicks;
        long deltaTicks = currentTicks - _lastFrameTicks;
        _lastFrameTicks = currentTicks;

        // Convert ticks to seconds (float, no allocation).
        float deltaTime = (float)deltaTicks / Stopwatch.Frequency;

        // Advance animation.
        _animator.Update(deltaTime);

        // Read current animated values (out params, no allocation).
        _animator.GetCurrentValues(
            out float width, out float height,
            out float cornerRadius, out float opacity, out float offsetY);

        // Apply to visual tree (Composition API calls, GPU-side).
        _visualTree.ApplyAnimatedValues(width, height, cornerRadius, opacity, offsetY);

        // Reposition the Win32 window to match animated size.
        // Center horizontally on the primary monitor.
        int screenWidth = Interop.NativeMethods.GetSystemMetrics(Interop.NativeMethods.SM_CXSCREEN);
        int physicalWidth = (int)width;
        int physicalHeight = (int)height;
        int x = (screenWidth - physicalWidth) / 2;
        int y = (int)offsetY;
        _window.Reposition(x, y, physicalWidth, physicalHeight);

        // Track frame time for perf monitoring.
        long frameEndTicks = _stopwatch.ElapsedTicks;
        _lastFrameTimeMs = (float)(frameEndTicks - currentTicks) / Stopwatch.Frequency * 1000f;

        // Update media progress
        var track = _mediaService.CurrentTrack;
        bool isMediaPlaying = track != null && track.Status == NovaIsland.Domain.Media.PlaybackStatus.Playing;
        if (track != null && isMediaPlaying && track.EndTime > TimeSpan.Zero)
        {
            var elapsed = DateTimeOffset.UtcNow - track.LastUpdatedTime;
            var currentPosition = track.Position + elapsed;
            if (currentPosition > track.EndTime) currentPosition = track.EndTime;
            float progress = (float)(currentPosition.TotalSeconds / track.EndTime.TotalSeconds);
            _visualTree.SetMediaProgress(progress);
        }
        else
        {
            _visualTree.SetMediaProgress(0f);
        }

        // Pause when settled to save CPU.
        // We only pause if media is NOT playing, because if media is playing we need to keep rendering the progress bar.
        if (_animator.IsSettled && !isMediaPlaying)
        {
            Stop();
            _logger.LogDebug("Animation settled and media not playing. Frame timer paused for zero idle CPU");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _frameTimer?.Dispose();
    }
}
