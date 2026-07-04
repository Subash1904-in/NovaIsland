using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Vortice.D3D11;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.WIC;
using Vortice.Mathematics;
using WinRT;

namespace NovaIsland.UI.Shell;

[GeneratedComInterface]
[Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
internal partial interface ICompositorInterop
{
    void CreateCompositionSurfaceForHandle(nint swapChain, out ICompositionSurface result);
    void CreateCompositionSurfaceForSwapChain(nint swapChain, out ICompositionSurface result);
    void CreateGraphicsDevice(nint renderingDevice, out CompositionGraphicsDevice result);
}

[GeneratedComInterface]
[Guid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6")]
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
    private readonly SpriteVisual _textVisual;
    private readonly CompositionSurfaceBrush _iconBrush;
    private readonly CompositionSurfaceBrush _textBrush;
    
    private readonly ID3D11Device _d3dDevice;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly ID2D1Device _d2dDevice;
    private readonly ID2D1DeviceContext _d2dContext;
    private readonly IDWriteFactory _dwriteFactory;
    private readonly IWICImagingFactory _wicFactory;
    private readonly CompositionGraphicsDevice _graphicsDevice;

    private CompositionDrawingSurface? _iconSurface;
    private CompositionDrawingSurface? _textSurface;
    private bool _disposed;

    // Cache for decoded bitmaps
    private readonly ConcurrentDictionary<string, ID2D1Bitmap> _bitmapCache = new();
    private string? _currentIconHash;
    private string _currentTitle = string.Empty;
    private string _currentSubtitle = string.Empty;

    public SpriteVisual IconVisual => _iconVisual;
    public SpriteVisual TextVisual => _textVisual;

    public IslandContentRenderer(Compositor compositor)
    {
        _compositor = compositor;
        
        // 1. Initialize Direct3D / DXGI
        D3D11.D3D11CreateDevice(
            null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, 
            null, out _d3dDevice).CheckError();
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();

        // 2. Initialize Direct2D / DirectWrite / WIC
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.SingleThreaded);
        _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
        _wicFactory = new IWICImagingFactory();

        // 3. Create Composition Graphics Device
        var compositorInterop = compositor.As<ICompositorInterop>();
        compositorInterop.CreateGraphicsDevice(_dxgiDevice.NativePointer, out _graphicsDevice);

        // 4. Setup Visuals
        _iconVisual = _compositor.CreateSpriteVisual();
        _iconBrush = _compositor.CreateSurfaceBrush();
        _iconVisual.Brush = _iconBrush;
        _iconVisual.Size = new Vector2(24f, 24f);
        _iconVisual.Offset = new Vector3(20f, 18f, 0f);

        _textVisual = _compositor.CreateSpriteVisual();
        _textBrush = _compositor.CreateSurfaceBrush();
        _textVisual.Brush = _textBrush;
        _textVisual.Size = new Vector2(300f, 60f);
        _textVisual.Offset = new Vector3(60f, 12f, 0f);
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
                    DrawIcon(cachedBitmap);
                }
                else
                {
                    // Asynchronous decode to avoid blocking hot path
                    Task.Run(() => 
                    {
                        try 
                        {
                            var bitmap = DecodeImage(iconBytes);
                            _bitmapCache[hash] = bitmap;
                            // Need to dispatch to UI thread or since D2D context is single threaded,
                            // we lock or just draw. We can invoke a method to draw on the correct thread.
                            // For this simplified example, we'll draw it directly if thread safety allows,
                            // or ideally enqueue to a dispatcher. We'll draw it.
                            lock (_d2dContext)
                            {
                                DrawIcon(bitmap);
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
        if (_textSurface == null)
        {
            _textSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(300, 60), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _textBrush.Surface = _textSurface;
        }

        var surfaceInterop = _textSurface.As<ICompositionDrawingSurfaceInterop>();
        Guid iid = typeof(IDXGISurface).GUID;
        surfaceInterop.BeginDraw(0, iid, out nint dxgiSurfacePtr, out _);
        
        using var dxgiSurface = new IDXGISurface(dxgiSurfacePtr);
        using var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface);
        
        lock (_d2dContext)
        {
            _d2dContext.Target = bitmap;
            _d2dContext.BeginDraw();
            _d2dContext.Clear(new Color4(0, 0, 0, 0));

            using var solidBrushWhite = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            using var solidBrushGray = _d2dContext.CreateSolidColorBrush(new Color4(0.8f, 0.8f, 0.8f, 1f));
            
            using var titleFormat = _dwriteFactory.CreateTextFormat("Segoe UI", 14f);
            titleFormat.FontWeight = FontWeight.SemiBold;
            titleFormat.WordWrapping = WordWrapping.NoWrap;

            using var subtitleFormat = _dwriteFactory.CreateTextFormat("Segoe UI", 12f);
            subtitleFormat.WordWrapping = WordWrapping.NoWrap;

            _d2dContext.DrawText(title, titleFormat, new System.Drawing.RectangleF(0, 0, 300, 20), solidBrushWhite);
            
            if (!string.IsNullOrEmpty(subtitle))
            {
                _d2dContext.DrawText(subtitle, subtitleFormat, new System.Drawing.RectangleF(0, 20, 300, 20), solidBrushGray);
            }

            _d2dContext.EndDraw();
            _d2dContext.Target = null;
        }
        
        surfaceInterop.EndDraw();
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
        surfaceInterop.BeginDraw(0, iid, out nint dxgiSurfacePtr, out _);
        
        using var dxgiSurface = new IDXGISurface(dxgiSurfacePtr);
        using var targetBitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface);
        
        _d2dContext.Target = targetBitmap;
        _d2dContext.BeginDraw();
        _d2dContext.Clear(new Color4(0, 0, 0, 0));
        _d2dContext.DrawBitmap(bitmap, new System.Drawing.RectangleF(0, 0, 24, 24), 1.0f, BitmapInterpolationMode.Linear);
        _d2dContext.EndDraw();
        _d2dContext.Target = null;
        
        surfaceInterop.EndDraw();
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
        _textSurface?.Dispose();
        _iconBrush.Dispose();
        _textBrush.Dispose();
        _iconVisual.Dispose();
        _textVisual.Dispose();
        
        _dwriteFactory.Dispose();
        _wicFactory.Dispose();
        _d2dContext.Dispose();
        _d2dDevice.Dispose();
        _d2dFactory.Dispose();
        _dxgiDevice.Dispose();
        _d3dDevice.Dispose();
    }
}
