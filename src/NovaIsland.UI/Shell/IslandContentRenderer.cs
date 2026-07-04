using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Vortice.Direct3D11;
using Vortice.Direct3D;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.WIC;
using Vortice.Mathematics;
using WinRT;

#pragma warning disable SYSLIB1051
#pragma warning disable CA1859

namespace NovaIsland.UI.Shell;

[GeneratedComInterface]
[Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
internal partial interface ICompositorInterop
{
    void CreateCompositionSurfaceForHandle(nint swapChain, out nint result);
    void CreateCompositionSurfaceForSwapChain(nint swapChain, out nint result);
    void CreateGraphicsDevice(nint renderingDevice, out nint result);
}

[GeneratedComInterface]
[Guid("FD04E6E3-FE0C-4C3C-AB19-A07601A576EE")]
internal partial interface ICompositionDrawingSurfaceInterop
{
    void BeginDraw(nint updateRect, in Guid iid, out nint updateObject, out System.Drawing.Point offset);
    void EndDraw();
    void Resize(System.Drawing.Size sizePixels);
    void Scroll(nint scrollRect, nint clipRect, int offsetX, int offsetY);
    void ResumeDraw();
    void SuspendDraw();
}

internal sealed class IslandContentRenderer : IDisposable
{
    private readonly Compositor _compositor;
    private readonly SpriteVisual _iconVisual;

    private readonly SpriteVisual _titleVisual;
    private readonly SpriteVisual _subtitleVisual;
    private readonly SpriteVisual _mediaControlsVisual;
    private readonly CompositionSurfaceBrush _iconBrush;
    private readonly CompositionSurfaceBrush _titleBrush;
    private readonly CompositionSurfaceBrush _subtitleBrush;
    private readonly CompositionSurfaceBrush _mediaControlsBrush;
    
    private readonly ID3D11Device _d3dDevice;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly ID2D1Device _d2dDevice;
    private readonly ID2D1DeviceContext _d2dContext;
    private readonly IDWriteFactory _dwriteFactory;
    private readonly IWICImagingFactory _wicFactory;
    private readonly CompositionGraphicsDevice _graphicsDevice;

    private CompositionDrawingSurface? _iconSurface;
    private CompositionDrawingSurface? _titleSurface;
    private CompositionDrawingSurface? _subtitleSurface;
    private CompositionDrawingSurface? _mediaControlsSurface;
    private bool _disposed;

    // Cache for decoded bitmaps
    private readonly ConcurrentDictionary<string, ID2D1Bitmap> _bitmapCache = new();
    private string? _currentIconHash;
    private string _currentTitle = string.Empty;
    private string _currentSubtitle = string.Empty;

    public SpriteVisual IconVisual => _iconVisual;
    public SpriteVisual TitleVisual => _titleVisual;
    public SpriteVisual SubtitleVisual => _subtitleVisual;
    public SpriteVisual MediaControlsVisual => _mediaControlsVisual;

    public IslandContentRenderer(Compositor compositor)
    {
        _compositor = compositor;
        
        // 1. Initialize Direct3D / DXGI
        var hr = D3D11.D3D11CreateDevice(
            null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, 
            null, out ID3D11Device? d3dDevice);
            
        if (hr.Failure || d3dDevice == null)
        {
            D3D11.D3D11CreateDevice(
                null, DriverType.Warp, DeviceCreationFlags.BgraSupport, 
                null, out d3dDevice).CheckError();
        }

        _d3dDevice = d3dDevice!;
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();

        // 2. Initialize Direct2D / DirectWrite / WIC
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.SingleThreaded);
        _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
        _wicFactory = new IWICImagingFactory();

        // 3. Create Composition Graphics Device
        var compositorInterop = compositor.As<ICompositorInterop>();
        compositorInterop.CreateGraphicsDevice(_dxgiDevice.NativePointer, out nint graphicsDevicePtr);
        _graphicsDevice = MarshalInterface<CompositionGraphicsDevice>.FromAbi(graphicsDevicePtr);

        // 4. Setup Visuals
        _iconVisual = _compositor.CreateSpriteVisual();
        _iconBrush = _compositor.CreateSurfaceBrush();
        _iconVisual.Brush = _iconBrush;
        _iconVisual.Size = new Vector2(24f, 24f);

        _titleVisual = _compositor.CreateSpriteVisual();
        _titleBrush = _compositor.CreateSurfaceBrush();
        _titleVisual.Brush = _titleBrush;
        _titleVisual.Size = new Vector2(300f, 24f);
        _titleVisual.Offset = new Vector3(60f, 0f, 0f);

        _subtitleVisual = _compositor.CreateSpriteVisual();
        _subtitleBrush = _compositor.CreateSurfaceBrush();
        _subtitleVisual.Brush = _subtitleBrush;
        _subtitleVisual.Size = new Vector2(300f, 20f);
        _subtitleVisual.Offset = new Vector3(60f, 0f, 0f);

        _mediaControlsVisual = _compositor.CreateSpriteVisual();
        _mediaControlsBrush = _compositor.CreateSurfaceBrush();
        _mediaControlsVisual.Brush = _mediaControlsBrush;
        _mediaControlsVisual.Size = new Vector2(300f, 40f);
        _mediaControlsVisual.Offset = new Vector3(50f, 200f, 0f);
        
        DrawMediaControls();
    }

    public void UpdateContent(string title, string subtitle, byte[]? iconBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool textChanged = title != _currentTitle || subtitle != _currentSubtitle;
        _currentTitle = title ?? "";
        _currentSubtitle = subtitle ?? "";

        if (textChanged)
        {
            DrawText(_currentTitle, _currentSubtitle);
        }

        if (iconBytes != null && iconBytes.Length > 0)
        {
            string hash = ComputeHash(iconBytes);
            if (hash != _currentIconHash)
            {
                _currentIconHash = hash;
                
                if (_bitmapCache.TryGetValue(hash, out var cachedBitmap))
                {
                    lock (_d2dContext)
                    {
                        DrawIcon(cachedBitmap);
                    }
                }
                else
                {
                    string capturedHash = hash;
                    Task.Run(() => 
                    {
                        try 
                        {
                            var bitmap = DecodeImage(iconBytes);
                            _bitmapCache[capturedHash] = bitmap;
                            
                            lock (_d2dContext)
                            {
                                if (_currentIconHash == capturedHash && !_disposed)
                                {
                                    DrawIcon(bitmap);
                                }
                            }
                        }
                        catch 
                        {
                            // Ignore decode failure
                        }
                    });
                }
            }
        }
        else
        {
            _currentIconHash = null;
            _iconBrush.Surface = null;
        }
    }

    private void DrawText(string title, string subtitle)
    {
        if (_titleSurface == null)
        {
            _titleSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(300, 24), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _titleBrush.Surface = _titleSurface;
        }

        if (_subtitleSurface == null)
        {
            _subtitleSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(300, 20), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _subtitleBrush.Surface = _subtitleSurface;
        }

        Guid iid = typeof(IDXGISurface).GUID;
        
        lock (_d2dContext)
        {
            using var solidBrushWhite = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            using var solidBrushGray = _d2dContext.CreateSolidColorBrush(new Color4(0.8f, 0.8f, 0.8f, 1f));
            
            using var titleFormat = _dwriteFactory.CreateTextFormat("Segoe UI", FontWeight.SemiBold, FontStyle.Normal, FontStretch.Normal, 14f);
            titleFormat.WordWrapping = WordWrapping.NoWrap;

            using var subtitleFormat = _dwriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 12f);
            subtitleFormat.WordWrapping = WordWrapping.NoWrap;

            // Draw title
            var titleInterop = _titleSurface.As<ICompositionDrawingSurfaceInterop>();
            titleInterop.BeginDraw(0, iid, out nint titleDxgiPtr, out _);
            Marshal.AddRef(titleDxgiPtr);
            using (var dxgiSurface = new IDXGISurface(titleDxgiPtr))
            using (var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface))
            {
                _d2dContext.Target = bitmap;
                _d2dContext.BeginDraw();
                _d2dContext.Clear(new Color4(0, 0, 0, 0));
                _d2dContext.DrawText(title, titleFormat, new Rect(0, 0, 300, 24), solidBrushWhite);
                _d2dContext.EndDraw();
                _d2dContext.Target = null;
            }
            titleInterop.EndDraw();

            // Draw subtitle
            var subtitleInterop = _subtitleSurface.As<ICompositionDrawingSurfaceInterop>();
            subtitleInterop.BeginDraw(0, iid, out nint subtitleDxgiPtr, out _);
            Marshal.AddRef(subtitleDxgiPtr);
            using (var dxgiSurface = new IDXGISurface(subtitleDxgiPtr))
            using (var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface))
            {
                _d2dContext.Target = bitmap;
                _d2dContext.BeginDraw();
                _d2dContext.Clear(new Color4(0, 0, 0, 0));
                if (!string.IsNullOrEmpty(subtitle))
                {
                    _d2dContext.DrawText(subtitle, subtitleFormat, new Rect(0, 0, 300, 20), solidBrushGray);
                }
                _d2dContext.EndDraw();
                _d2dContext.Target = null;
            }
            subtitleInterop.EndDraw();
        }
    }

    private void DrawMediaControls()
    {
        if (_mediaControlsSurface == null)
        {
            _mediaControlsSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(300, 40), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _mediaControlsBrush.Surface = _mediaControlsSurface;
        }

        Guid iid = typeof(IDXGISurface).GUID;
        
        lock (_d2dContext)
        {
            using var solidBrushWhite = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            using var iconFormat = _dwriteFactory.CreateTextFormat("Segoe Fluent Icons", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            iconFormat.TextAlignment = TextAlignment.Center;
            
            var controlsInterop = _mediaControlsSurface.As<ICompositionDrawingSurfaceInterop>();
            controlsInterop.BeginDraw(0, iid, out nint dxgiPtr, out _);
            Marshal.AddRef(dxgiPtr);
            using (var dxgiSurface = new IDXGISurface(dxgiPtr))
            using (var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface))
            {
                _d2dContext.Target = bitmap;
                _d2dContext.BeginDraw();
                _d2dContext.Clear(new Color4(0, 0, 0, 0));
                
                // Draw Previous, Play/Pause, Next
                // \uE892 = Prev, \uE768 = Play, \uE893 = Next
                _d2dContext.DrawText("\uE892", iconFormat, new Rect(0, 0, 100, 40), solidBrushWhite);
                _d2dContext.DrawText("\uE768", iconFormat, new Rect(100, 0, 100, 40), solidBrushWhite);
                _d2dContext.DrawText("\uE893", iconFormat, new Rect(200, 0, 100, 40), solidBrushWhite);
                
                _d2dContext.EndDraw();
                _d2dContext.Target = null;
            }
            controlsInterop.EndDraw();
        }
    }

    public void UpdateLayout(float currentHeight, IslandInteractionState interactionState)
    {
        // Base layout logic on height.
        float idleHeight = 16f;

        if (currentHeight <= idleHeight + 0.1f)
        {
            // Fully idle (black pill)
            _iconVisual.Opacity = 0f;
            _titleVisual.Opacity = 0f;
            _subtitleVisual.Opacity = 0f;
            _mediaControlsVisual.Opacity = 0f;
        }
        else if (currentHeight <= 60.1f)
        {
            // Peek (height up to 60)
            float t = Math.Clamp((currentHeight - idleHeight) / (60f - idleHeight), 0f, 1f); 
            _iconVisual.Opacity = t;
            _titleVisual.Opacity = t;
            _subtitleVisual.Opacity = t;
            _mediaControlsVisual.Opacity = 0f; // Hidden in peek
            
            float iconTop = 16f;
            _iconVisual.Offset = new Vector3(20f, iconTop, 0f);
            
            _titleVisual.Offset = new Vector3(60f, 8f, 0f);
            _subtitleVisual.Offset = new Vector3(60f, 28f, 0f);
        }
        else
        {
            // FullExpanded (height > 60)
            float t = Math.Clamp((currentHeight - 60f) / (320f - 60f), 0f, 1f);
            _iconVisual.Opacity = 1f;
            _titleVisual.Opacity = 1f;
            _subtitleVisual.Opacity = 1f;
            _mediaControlsVisual.Opacity = t;
            
            // Move visuals around for expanded state
            // Icon moves down and gets some margin, maybe scales if we used transforms.
            _iconVisual.Offset = new Vector3(20f, 16f + (20f * t), 0f);
            _titleVisual.Offset = new Vector3(60f, 8f + (20f * t), 0f);
            _subtitleVisual.Offset = new Vector3(60f, 28f + (20f * t), 0f);
            
            // Media controls fade in and position at the bottom
            _mediaControlsVisual.Offset = new Vector3(50f, 200f, 0f);
        }
    }

    private void DrawIcon(ID2D1Bitmap bitmap)
    {
        if (_iconSurface == null)
        {
            _iconSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(24, 24), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _iconBrush.Surface = _iconSurface;
        }

        var surfaceInterop = _iconSurface.As<ICompositionDrawingSurfaceInterop>();
        Guid iid = typeof(IDXGISurface).GUID;
        
        lock (_d2dContext)
        {
            surfaceInterop.BeginDraw(0, iid, out nint dxgiSurfacePtr, out _);
            
            Marshal.AddRef(dxgiSurfacePtr);
            
            using var dxgiSurface = new IDXGISurface(dxgiSurfacePtr);
            using var targetBitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface);
            
            _d2dContext.Target = targetBitmap;
            _d2dContext.BeginDraw();
            _d2dContext.Clear(new Color4(0, 0, 0, 0));
            ((ID2D1RenderTarget)_d2dContext).DrawBitmap(bitmap, 1.0f, Vortice.Direct2D1.BitmapInterpolationMode.Linear, new Rect(0, 0, 24, 24));
            _d2dContext.EndDraw();
            _d2dContext.Target = null;
            
            surfaceInterop.EndDraw();
        }
    }

    private ID2D1Bitmap DecodeImage(byte[] imageBytes)
    {
        using var stream = _wicFactory.CreateStream(imageBytes);
        using var decoder = _wicFactory.CreateDecoderFromStream(stream, DecodeOptions.CacheOnLoad);
        using var frame = decoder.GetFrame(0);
        using var converter = _wicFactory.CreateFormatConverter();
        converter.Initialize(frame, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapDitherType.None, null, 0.0, BitmapPaletteType.Custom);
        
        lock (_d2dContext)
        {
            return _d2dContext.CreateBitmapFromWicBitmap(converter);
        }
    }

    private static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToBase64String(hash);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();

        _iconSurface?.Dispose();
        _titleSurface?.Dispose();
        _subtitleSurface?.Dispose();
        _mediaControlsSurface?.Dispose();
        _iconBrush.Dispose();
        _titleBrush.Dispose();
        _subtitleBrush.Dispose();
        _mediaControlsBrush.Dispose();
        _iconVisual.Dispose();
        _titleVisual.Dispose();
        _subtitleVisual.Dispose();
        _mediaControlsVisual.Dispose();
        
        _dwriteFactory.Dispose();
        _wicFactory.Dispose();
        _d2dContext.Dispose();
        _d2dDevice.Dispose();
        _d2dFactory.Dispose();
        _dxgiDevice.Dispose();
        _d3dDevice.Dispose();
    }
}
