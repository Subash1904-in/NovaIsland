using System;
using System.Diagnostics;
using System.Numerics;
using Windows.UI.Composition;
using NovaIsland.Domain.Media;
using NovaIsland.Application.Modules;
using static NovaIsland.UI.Interop.NativeMethods;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Renders the expanded detail panel showing Now Playing, Clipboard, and Notifications.
/// </summary>
internal sealed class IslandDetailPanel : IDisposable
{
    private readonly Compositor _compositor;
    private readonly ContainerVisual _rootVisual;
    private readonly IslandHitTestRegistry _hitTestRegistry;
    private readonly IMediaService _mediaService;
    private readonly NotificationModule _notificationModule;
    private bool _disposed;

    public ContainerVisual RootVisual => _rootVisual;

    public IslandDetailPanel(Compositor compositor, IslandHitTestRegistry hitTestRegistry, IMediaService mediaService, NotificationModule notificationModule)
    {
        _compositor = compositor;
        _hitTestRegistry = hitTestRegistry;
        _mediaService = mediaService;
        _notificationModule = notificationModule;

        _rootVisual = _compositor.CreateContainerVisual();
        _rootVisual.Opacity = 0f; // Hidden by default
    }

    /// <summary>
    /// Updates the layout and populates the hit test registry for the current items.
    /// </summary>
    public void UpdateLayout(float width, float height, IslandInteractionState interactionState)
    {
        if (interactionState != IslandInteractionState.FullExpanded)
        {
            _rootVisual.Opacity = 0f;
            return;
        }

        _rootVisual.Opacity = 1f;

        // In a full implementation, this would draw the rows using Composition surface brushes.
        // For now, we set up the hit-test bounds based on fixed offsets.
        
        // Media controls are drawn at Offset (50, 200). Size is (300, 40).
        // Hit regions for Previous, Play/Pause, Next (each is 100 wide)
        float controlsY = 200f;
        float controlsX = 50f;
        
        var prevRect = new System.Drawing.RectangleF(controlsX, controlsY, 100f, 40f);
        _hitTestRegistry.Register(prevRect, () => 
        {
            _ = _mediaService.PreviousAsync();
        });

        var playPauseRect = new System.Drawing.RectangleF(controlsX + 100f, controlsY, 100f, 40f);
        _hitTestRegistry.Register(playPauseRect, () => 
        {
            _ = _mediaService.PlayPauseAsync();
        });

        var nextRect = new System.Drawing.RectangleF(controlsX + 200f, controlsY, 100f, 40f);
        _hitTestRegistry.Register(nextRect, () => 
        {
            _ = _mediaService.NextAsync();
        });

        // More rows could be added here for clipboard/notifications...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rootVisual.Dispose();
    }
}
