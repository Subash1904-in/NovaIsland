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
        
        // We'll create a dummy "Now Playing" row hit rect just below the compact area.
        float rowHeight = 40f;
        float currentY = 50f; // Start below the main title/subtitle

        // Example: Now Playing row
        var nowPlayingRect = new System.Drawing.RectangleF(0, currentY, width, rowHeight);
        _hitTestRegistry.Register(nowPlayingRect, () => 
        {
            // Try to bring media player to foreground
            // We use FindWindow as a placeholder for actual player resolution
            nint hwnd = FindWindowW(null, "Spotify Premium");
            if (hwnd == 0) hwnd = FindWindowW(null, "Media Player");
            
            if (hwnd != 0)
            {
                SetForegroundWindow(hwnd);
            }
        });

        currentY += rowHeight;

        // More rows could be added here for clipboard/notifications...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rootVisual.Dispose();
    }
}
