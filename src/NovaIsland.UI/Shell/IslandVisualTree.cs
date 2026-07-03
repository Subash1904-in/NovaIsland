using System.Numerics;
using Microsoft.Extensions.Logging;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using NovaIsland.UI.Interop;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Manages the <see cref="Windows.UI.Composition"/> visual tree bound to the
/// island's Win32 window. Owns the <see cref="Compositor"/>, root visual,
/// and visual layers (background, content, accent).
/// </summary>
/// <remarks>
/// <para>
/// All visual property updates (size, corner radius, opacity, offset) are
/// Composition API calls that execute on the GPU compositor thread — they
/// do NOT allocate on the managed heap and do NOT block the UI thread.
/// </para>
/// <para>
/// <b>No XAML anywhere</b>. This class uses only
/// <see cref="SpriteVisual"/>, <see cref="ContainerVisual"/>, and
/// <see cref="CompositionRoundedRectangleGeometry"/> from the Composition API.
/// </para>
/// </remarks>
internal sealed class IslandVisualTree : IDisposable
{
    private readonly ILogger _logger;
    private Compositor? _compositor;
    private DesktopWindowTarget? _target;
    private ContainerVisual? _rootVisual;
    private CompositionRoundedRectangleGeometry? _shapeGeometry;
    private ShapeVisual? _shapeVisual;
    private CompositionSpriteShape? _spriteShape;
    
    private CompositionRoundedRectangleGeometry? _progressBarGeometry;
    private CompositionSpriteShape? _progressBarShape;
    private bool _disposed;

    /// <summary>
    /// Gets the Compositor instance, or null if not yet initialized.
    /// </summary>
    internal Compositor? Compositor => _compositor;

    /// <summary>
    /// Initializes a new <see cref="IslandVisualTree"/>.
    /// Call <see cref="Initialize"/> to create the visual tree.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    internal IslandVisualTree(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates the Composition visual tree and binds it to the specified HWND.
    /// Must be called on the STA thread after the window is created.
    /// </summary>
    /// <param name="hwnd">The native window handle to bind to.</param>
    /// <param name="initialWidth">Initial width in logical pixels.</param>
    /// <param name="initialHeight">Initial height in logical pixels.</param>
    internal void Initialize(nint hwnd, float initialWidth, float initialHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CompositionInterop.CreateDesktopWindowTarget(hwnd, out var compositor, out var target, isTopmost: false);
        _compositor = compositor;
        _target = target;

        // Create root container visual.
        _rootVisual = compositor.CreateContainerVisual();
        _rootVisual.Size = new Vector2(initialWidth, initialHeight);
        target.Root = _rootVisual;

        // Create the island shape using a rounded rectangle geometry.
        _shapeGeometry = compositor.CreateRoundedRectangleGeometry();
        _shapeGeometry.Size = new Vector2(initialWidth, initialHeight);
        _shapeGeometry.CornerRadius = new Vector2(20f, 20f);

        _shapeVisual = compositor.CreateShapeVisual();
        _shapeVisual.Size = new Vector2(initialWidth, initialHeight);

        _spriteShape = compositor.CreateSpriteShape(_shapeGeometry);
        _spriteShape.FillBrush = compositor.CreateColorBrush(Windows.UI.Color.FromArgb(200, 32, 32, 32));

        // Create the progress bar shape
        _progressBarGeometry = compositor.CreateRoundedRectangleGeometry();
        _progressBarGeometry.CornerRadius = new Vector2(2f, 2f);
        _progressBarGeometry.Size = new Vector2(0f, 4f);

        _progressBarShape = compositor.CreateSpriteShape(_progressBarGeometry);
        _progressBarShape.FillBrush = compositor.CreateColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        _progressBarShape.Offset = new Vector2(20f, initialHeight - 10f); // Position at bottom

        _shapeVisual.Shapes.Add(_spriteShape);
        _shapeVisual.Shapes.Add(_progressBarShape);
        _rootVisual.Children.InsertAtTop(_shapeVisual);

        _logger.LogDebug("Composition visual tree initialized: {Width}x{Height}", initialWidth, initialHeight);
    }

    /// <summary>
    /// Updates the visual tree size. Called from the frame-pacing loop.
    /// </summary>
    /// <param name="width">New width in logical pixels.</param>
    /// <param name="height">New height in logical pixels.</param>
    /// <remarks>
    /// ZERO-ALLOC on the managed heap: Vector2 is a value type, and Composition
    /// property setters marshal directly to the compositor thread.
    /// </remarks>
    internal void SetSize(float width, float height)
    {
        if (_rootVisual is null || _shapeGeometry is null || _shapeVisual is null) return;

        var size = new Vector2(width, height);
        _rootVisual.Size = size;
        _shapeVisual.Size = size;
        _shapeGeometry.Size = size;
    }

    /// <summary>
    /// Updates the corner radius of the island shape.
    /// </summary>
    /// <param name="radius">New corner radius in logical pixels.</param>
    internal void SetCornerRadius(float radius)
    {
        if (_shapeGeometry is null) return;
        _shapeGeometry.CornerRadius = new Vector2(radius, radius);
    }

    /// <summary>
    /// Updates the opacity of the root visual.
    /// </summary>
    /// <param name="opacity">New opacity (0.0–1.0).</param>
    internal void SetOpacity(float opacity)
    {
        if (_rootVisual is null) return;
        _rootVisual.Opacity = opacity;
    }

    /// <summary>
    /// Updates the vertical offset of the root visual.
    /// </summary>
    /// <param name="offsetY">New Y offset in logical pixels.</param>
    internal void SetOffset(float offsetY)
    {
        if (_rootVisual is null) return;
        _rootVisual.Offset = new Vector3(0f, offsetY, 0f);
    }

    /// <summary>
    /// Applies all animated values in a single batch call.
    /// This is the primary method called from the frame-pacing hot loop.
    /// </summary>
    /// <param name="width">Current animated width.</param>
    /// <param name="height">Current animated height.</param>
    /// <param name="cornerRadius">Current animated corner radius.</param>
    /// <param name="opacity">Current animated opacity.</param>
    /// <param name="offsetY">Current animated vertical offset.</param>
    /// <remarks>
    /// ZERO-ALLOC: All Vector2/Vector3 values are stack-allocated value types.
    /// Composition property setters are lightweight COM interop calls.
    /// </remarks>
    internal void ApplyAnimatedValues(float width, float height, float cornerRadius, float opacity, float offsetY)
    {
        if (_rootVisual is null || _shapeGeometry is null || _shapeVisual is null) return;

        var size = new Vector2(width, height);
        _rootVisual.Size = size;
        _rootVisual.Opacity = opacity;
        _rootVisual.Offset = new Vector3(0f, offsetY, 0f);
        _shapeVisual.Size = size;
        _shapeGeometry.Size = size;
        _shapeGeometry.CornerRadius = new Vector2(cornerRadius, cornerRadius);

        // Keep progress bar at the bottom
        if (_progressBarShape != null)
        {
            _progressBarShape.Offset = new Vector2(20f, height - 10f);
        }
    }

    /// <summary>
    /// Updates the media progress bar width based on the normalized progress (0.0 to 1.0).
    /// </summary>
    /// <param name="progress">Normalized progress (0.0 to 1.0).</param>
    internal void SetMediaProgress(float progress)
    {
        if (_progressBarGeometry == null || _shapeVisual == null) return;
        
        // Progress bar spans the width minus padding (20f on each side)
        float maxProgressBarWidth = Math.Max(0f, _shapeVisual.Size.X - 40f);
        float currentWidth = maxProgressBarWidth * Math.Clamp(progress, 0f, 1f);
        
        _progressBarGeometry.Size = new Vector2(currentWidth, 4f);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _spriteShape = null;
        _shapeVisual = null;
        _shapeGeometry = null;
        _rootVisual = null;
        _target?.Dispose();
        _target = null;
        _compositor?.Dispose();
        _compositor = null;
    }
}
