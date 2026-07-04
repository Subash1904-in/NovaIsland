using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaIsland.UI.Animation;
using NovaIsland.UI.DpiAwareness;
using NovaIsland.UI.FramePacing;
using static NovaIsland.UI.Interop.NativeMethods;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Hosted service that orchestrates the island shell lifecycle:
/// window creation, composition binding, animation loop, DPI handling,
/// and state transitions.
/// </summary>
/// <remarks>
/// <para>
/// The shell runs on a dedicated STA thread because Win32 window creation
/// and message loops require single-threaded apartment threading. The hosted
/// service manages the thread lifecycle and coordinates shutdown.
/// </para>
/// <para>
/// Other modules trigger state transitions by calling <see cref="TransitionTo"/>
/// which posts a message to the shell thread (thread-safe, non-blocking).
/// </para>
/// </remarks>
public sealed class IslandShellService : IHostedService, IDisposable
{
    private readonly ILogger<IslandShellService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IslandSettings _settings;
    private readonly IIslandAnimator _animator;

    private Thread? _shellThread;
    private IslandWindow? _window;
    private IslandVisualTree? _visualTree;
    private FramePacingService? _framePacing;
    private DisplayRefreshDetector? _refreshDetector;
    private PerMonitorDpiHelper? _dpiHelper;
    private readonly NovaIsland.Domain.Media.IMediaService _mediaService;
    private readonly NovaIsland.Application.Modules.NotificationModule _notificationModule;

    private volatile bool _isStarted;
    private readonly ManualResetEventSlim _windowCreated = new(false);

    /// <summary>
    /// Initializes a new <see cref="IslandShellService"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="loggerFactory">Logger factory to create child loggers on the STA thread.</param>
    /// <param name="settings">Island configuration settings.</param>
    /// <param name="animator">The animation controller (spring or reduced-motion).</param>
    public IslandShellService(
        ILogger<IslandShellService> logger,
        ILoggerFactory loggerFactory,
        IOptions<IslandSettings> settings,
        IIslandAnimator animator,
        NovaIsland.Domain.Media.IMediaService mediaService,
        NovaIsland.Application.Modules.NotificationModule notificationModule)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settings = settings.Value;
        _animator = animator;
        _mediaService = mediaService;
        _notificationModule = notificationModule;
    }

    /// <summary>
    /// Gets a value indicating whether the shell is running.
    /// </summary>
    public bool IsRunning => _isStarted;

    /// <summary>
    /// Transitions the island to a new state. Thread-safe — can be called from any thread.
    /// Posts a message to the shell's Win32 message loop.
    /// </summary>
    /// <param name="target">The new target state.</param>
    public void TransitionTo(IslandState target)
    {
        if (_window is null || !_isStarted) return;

        _animator.TransitionTo(target);
        _framePacing?.Resume();

        _logger.LogInformation("Island transition requested: {Target}", target);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Island shell service starting...");

        // Apply user-configured dimension overrides.
        _settings.ApplyDimensionOverrides();

        // Start the shell on a dedicated STA thread.
        _shellThread = new Thread(ShellThreadProc)
        {
            Name = "NovaIsland.ShellThread",
            IsBackground = true,
        };
        _shellThread.SetApartmentState(ApartmentState.STA);
        _shellThread.Start();

        _notificationModule.OnAlertTriggered += HandleNotificationAlert;

        // Wait for window creation to complete before reporting started.
        _windowCreated.Wait(TimeSpan.FromSeconds(10), cancellationToken);
        _isStarted = true;

        _logger.LogInformation("Island shell service started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Island shell service stopping...");

        _isStarted = false;
        _framePacing?.Stop();

        _notificationModule.OnAlertTriggered -= HandleNotificationAlert;

        // Post WM_QUIT to terminate the message loop.
        _window?.PostQuit();

        // Wait for the shell thread to exit cleanly.
        if (_shellThread?.Join(TimeSpan.FromSeconds(5)) == false)
        {
            _logger.LogWarning("Shell thread did not exit within timeout");
        }

        _logger.LogInformation("Island shell service stopped");
        return Task.CompletedTask;
    }

    private void HandleNotificationAlert(object? sender, NovaIsland.Domain.Notifications.NotificationMessage msg)
    {
        // Trigger alert state
        TransitionTo(IslandState.Alert);

        // Then transition back to compact after 3 seconds
        Task.Delay(3000).ContinueWith(_ => TransitionTo(IslandState.Compact));
    }

    /// <summary>
    /// Entry point for the dedicated STA shell thread.
    /// Creates the window, initializes composition, starts frame pacing,
    /// and runs the Win32 message loop.
    /// </summary>
    private void ShellThreadProc()
    {
        try
        {
            // Initialize DPI helper.
            _dpiHelper = new PerMonitorDpiHelper(_loggerFactory.CreateLogger<PerMonitorDpiHelper>());

            // Detect display refresh rate.
            _refreshDetector = new DisplayRefreshDetector(_loggerFactory.CreateLogger<DisplayRefreshDetector>());
            _refreshDetector.Detect();

            // Get initial state descriptor.
            ref readonly var initialDesc = ref IslandStateDescriptors.GetDescriptor(_settings.InitialState);

            // Compute initial position (centered, DPI-scaled).
            _dpiHelper.ComputePositionCentered(
                0, initialDesc.Width, initialDesc.Height, initialDesc.OffsetY,
                out int x, out int y, out int physWidth, out int physHeight);

            // Create the Win32 window.
            _window = new IslandWindow(_loggerFactory.CreateLogger<IslandWindow>());
            _window.SetDisplayChangeCallback(OnDisplayChange);
            _window.SetDpiChangedCallback(OnDpiChanged);
            _window.Create(physWidth, physHeight, x, y);

            if (_window.Hwnd == 0)
            {
                _logger.LogError("Failed to create island window. Shell thread exiting");
                _windowCreated.Set();
                return;
            }

            // Initialize composition visual tree.
            _visualTree = new IslandVisualTree(_loggerFactory.CreateLogger<IslandVisualTree>());
            _visualTree.Initialize(_window.Hwnd, initialDesc.Width, initialDesc.Height);

            // Query initial DPI.
            _dpiHelper.QueryDpi(_window.Hwnd);

            // Create frame pacing service.
            _framePacing = new FramePacingService(
                _loggerFactory.CreateLogger<FramePacingService>(),
                _animator, _visualTree, _window, _refreshDetector, _mediaService);

            // Signal that window creation is complete.
            _windowCreated.Set();

            // Start the animation loop (will auto-pause when settled).
            _framePacing.Start();

            // Run the Win32 message loop (blocks until WM_QUIT).
            _window.RunMessageLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Island shell thread encountered a fatal error");
            _windowCreated.Set(); // Ensure StartAsync doesn't hang.
        }
        finally
        {
            _framePacing?.Stop();
            _visualTree?.Dispose();
            _window?.Dispose();
        }
    }

    /// <summary>
    /// Handles WM_DISPLAYCHANGE — re-detects refresh rate and repositions.
    /// </summary>
    private void OnDisplayChange()
    {
        _refreshDetector?.Detect();
        if (_window is not null)
        {
            _dpiHelper?.HandleDisplayChange(_window.Hwnd);
        }
        _logger.LogInformation("Display change handled. Refresh rate: {Hz} Hz", _refreshDetector?.RefreshRateHz);
    }

    /// <summary>
    /// Handles WM_DPICHANGED — updates scale factor and repositions.
    /// </summary>
    /// <param name="newDpi">The new DPI value.</param>
    private void OnDpiChanged(uint newDpi)
    {
        _dpiHelper?.UpdateDpi(newDpi);

        // Reposition with new DPI scaling.
        _animator.GetCurrentValues(out float w, out float h, out _, out _, out float offsetY);
        if (_dpiHelper is not null && _window is not null)
        {
            _dpiHelper.ComputePositionCentered(_window.Hwnd, w, h, offsetY,
                out int x, out int y, out int pw, out int ph);
            _window.Reposition(x, y, pw, ph);
        }

        _logger.LogInformation("DPI changed to {Dpi}. Island repositioned", newDpi);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _windowCreated.Dispose();
    }
}
