using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
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
using NovaIsland.Domain.Media;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Clipboard;
using NovaIsland.Domain.Widgets;
using NovaIsland.Domain.Notifications;
using static NovaIsland.UI.Interop.NativeMethods;

#pragma warning disable SYSLIB1051
#pragma warning disable CA1859

namespace NovaIsland.UI.Shell;

internal sealed class IslandDetailPanel : IDisposable
{
    private readonly Compositor _compositor;
    private readonly ContainerVisual _rootVisual;
    private readonly IslandHitTestRegistry _hitTestRegistry;
    private readonly IMediaService _mediaService;
    private readonly NotificationModule _notificationModule;
    private readonly IClipboardService _clipboardService;
    private readonly IEnumerable<IWidget> _widgets;
    
    private readonly SpriteVisual _surfaceVisual;
    private readonly CompositionSurfaceBrush _surfaceBrush;
    private CompositionDrawingSurface? _drawingSurface;

    private readonly ID3D11Device _d3dDevice;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly ID2D1Device _d2dDevice;
    private readonly ID2D1DeviceContext _d2dContext;
    private readonly IDWriteFactory _dwriteFactory;
    private readonly IWICImagingFactory _wicFactory;
    private readonly CompositionGraphicsDevice _graphicsDevice;

    private bool _disposed;
    private IslandInteractionState _lastState = IslandInteractionState.Idle;

    private readonly ConcurrentDictionary<string, ID2D1Bitmap> _iconCache = new();

    private List<ClipboardEntry> _clipboardEntries = new();
    private List<NotificationMessage> _notifications = new();

    public ContainerVisual RootVisual => _rootVisual;

    public IslandDetailPanel(
        Compositor compositor, 
        IslandHitTestRegistry hitTestRegistry, 
        IMediaService mediaService, 
        NotificationModule notificationModule,
        IClipboardService clipboardService,
        IEnumerable<IWidget> widgets)
    {
        _compositor = compositor;
        _hitTestRegistry = hitTestRegistry;
        _mediaService = mediaService;
        _notificationModule = notificationModule;
        _clipboardService = clipboardService;
        _widgets = widgets;

        _rootVisual = _compositor.CreateContainerVisual();
        _rootVisual.Opacity = 0f;

        // Initialize Direct3D / DXGI
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

        // Initialize Direct2D / DirectWrite / WIC
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.SingleThreaded);
        _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
        _wicFactory = new IWICImagingFactory();

        // Create Composition Graphics Device
        var compositorInterop = compositor.As<ICompositorInterop>();
        compositorInterop.CreateGraphicsDevice(_dxgiDevice.NativePointer, out nint graphicsDevicePtr);
        _graphicsDevice = MarshalInterface<CompositionGraphicsDevice>.FromAbi(graphicsDevicePtr);

        _surfaceVisual = _compositor.CreateSpriteVisual();
        _surfaceBrush = _compositor.CreateSurfaceBrush();
        _surfaceVisual.Brush = _surfaceBrush;
        _rootVisual.Children.InsertAtTop(_surfaceVisual);
        
        // Listen to notifications to update state if expanded
        _notificationModule.OnAlertTriggered += OnNotificationReceived;
    }

    private void OnNotificationReceived(object? sender, NotificationMessage e)
    {
        lock (_notifications)
        {
            _notifications.Insert(0, e);
            if (_notifications.Count > 3) _notifications.RemoveAt(3);
        }
        
        if (_lastState == IslandInteractionState.FullExpanded)
        {
            _ = RenderSurfaceAsync();
        }
    }

    public void UpdateLayout(float width, float height, IslandInteractionState interactionState)
    {
        if (interactionState != IslandInteractionState.FullExpanded)
        {
            _rootVisual.Opacity = 0f;
            _lastState = interactionState;
            return;
        }

        _rootVisual.Opacity = 1f;
        _surfaceVisual.Size = new Vector2(width, height);
        
        // HitTest registry leak fix: Only rebuild when transitioning INTO FullExpanded
        if (_lastState != IslandInteractionState.FullExpanded)
        {
            _hitTestRegistry.Clear();
            _ = FetchDataAndRenderAsync(width, height);
        }

        _lastState = interactionState;
    }

    private async Task FetchDataAndRenderAsync(float width, float height)
    {
        try
        {
            var history = await _clipboardService.GetHistoryAsync(3);
            _clipboardEntries = history.ToList();
            await RenderSurfaceAsync(width, height);
        }
        catch 
        {
            // Ignore fetch failures
        }
    }

    private async Task RenderSurfaceAsync(float width = 360f, float height = 320f)
    {
        if (_disposed) return;

        if (_drawingSurface == null || _drawingSurface.Size.Width != width || _drawingSurface.Size.Height != height)
        {
            _drawingSurface?.Dispose();
            _drawingSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(width, height), 
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            _surfaceBrush.Surface = _drawingSurface;
        }

        Guid iid = typeof(IDXGISurface).GUID;
        
        await Task.Run(() => 
        {
            lock (_d2dContext)
            {
                var surfaceInterop = _drawingSurface.As<ICompositionDrawingSurfaceInterop>();
                surfaceInterop.BeginDraw(0, iid, out nint dxgiPtr, out _);
                Marshal.AddRef(dxgiPtr);
                
                using (var dxgiSurface = new IDXGISurface(dxgiPtr))
                using (var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface))
                {
                    _d2dContext.Target = bitmap;
                    _d2dContext.BeginDraw();
                    _d2dContext.Clear(new Color4(0, 0, 0, 0));

                    DrawContent(width, height);

                    _d2dContext.EndDraw();
                    _d2dContext.Target = null;
                }
                surfaceInterop.EndDraw();
            }
        });
    }

    private void DrawContent(float width, float height)
    {
        using var whiteBrush = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        using var grayBrush = _d2dContext.CreateSolidColorBrush(new Color4(0.7f, 0.7f, 0.7f, 1f));
        using var textFormat = _dwriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 14f);
        textFormat.WordWrapping = WordWrapping.NoWrap;
        
        using var iconFormat = _dwriteFactory.CreateTextFormat("Segoe Fluent Icons", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 20f);
        iconFormat.TextAlignment = TextAlignment.Center;
        
        float y = 60f; // Start below the main header

        // 1. Widgets Row
        foreach (var widget in _widgets)
        {
            string summary = widget.GetSummaryText();
            _d2dContext.DrawText(summary, textFormat, new Rect(20, y, width - 40, 20), whiteBrush);
            y += 25f;
        }

        y += 10f; // spacing

        // 2. Clipboard Row
        if (_clipboardEntries.Count > 0)
        {
            _d2dContext.DrawText("Recent Clipboard", textFormat, new Rect(20, y, width - 40, 20), grayBrush);
            y += 25f;

            foreach (var entry in _clipboardEntries)
            {
                string text = entry.Content ?? "Image/File copied";
                if (text.Length > 40) text = string.Concat(text.AsSpan(0, 37), "...");
                
                var bmp = GetIconForApp(entry.SourceAppPath);
                if (bmp != null)
                {
                    ((ID2D1RenderTarget)_d2dContext).DrawBitmap(bmp, 1.0f, Vortice.Direct2D1.BitmapInterpolationMode.Linear, new Rect(20, y, 16, 16));
                }
                
                _d2dContext.DrawText(text, textFormat, new Rect(45, y, width - 60, 20), whiteBrush);
                
                // Register hit test for dynamic app launch
                string? appPath = entry.SourceAppPath;
                _hitTestRegistry.Register(new System.Drawing.RectangleF(20, y, width - 40, 20), () => 
                {
                    LaunchOrForegroundApp(appPath);
                });

                y += 25f;
            }
        }
        
        y += 10f;

        // 3. Notifications Row
        lock (_notifications)
        {
            if (_notifications.Count > 0)
            {
                _d2dContext.DrawText("Recent Notifications", textFormat, new Rect(20, y, width - 40, 20), grayBrush);
                y += 25f;

                foreach (var notif in _notifications)
                {
                    var bmp = GetIconForApp(notif.SourceAppPath);
                    if (bmp != null)
                    {
                        ((ID2D1RenderTarget)_d2dContext).DrawBitmap(bmp, 1.0f, Vortice.Direct2D1.BitmapInterpolationMode.Linear, new Rect(20, y, 16, 16));
                    }
                    
                    _d2dContext.DrawText($"{notif.AppName}: {notif.Title}", textFormat, new Rect(45, y, width - 60, 20), whiteBrush);
                    
                    string? appPath = notif.SourceAppPath;
                    _hitTestRegistry.Register(new System.Drawing.RectangleF(20, y, width - 40, 20), () => 
                    {
                        LaunchOrForegroundApp(appPath);
                    });

                    y += 25f;
                }
            }
        }
    }

    private static void LaunchOrForegroundApp(string? appPath)
    {
        if (string.IsNullOrEmpty(appPath)) return;
        // Launch or bring to foreground based on path
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true
            });
            if (p != null && p.MainWindowHandle != 0)
            {
                SetForegroundWindow(p.MainWindowHandle);
            }
        }
        catch
        {
            // Ignore execution failures
        }
    }

    private ID2D1Bitmap? GetIconForApp(string? appPath)
    {
        if (string.IsNullOrEmpty(appPath)) return null;
        if (_iconCache.TryGetValue(appPath, out var bitmap)) return bitmap;
        
        SHFILEINFO shinfo = new();
        var hImg = SHGetFileInfoW(appPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);
        if (hImg != 0 && shinfo.hIcon != 0)
        {
            try
            {
                // Vortice WIC doesn't support CreateBitmapFromHIcon natively? 
                // We will try using CreateBitmapFromHICON. If it fails to compile we will adjust.
                var wicBitmap = _wicFactory.CreateBitmapFromHICON(shinfo.hIcon);
                using var converter = _wicFactory.CreateFormatConverter();
                converter.Initialize(wicBitmap, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapDitherType.None, null, 0.0, BitmapPaletteType.Custom);
                
                bitmap = _d2dContext.CreateBitmapFromWicBitmap(converter);
                _iconCache[appPath] = bitmap;
                wicBitmap.Dispose();
                return bitmap;
            }
            catch
            {
                // Ignore conversion errors
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _notificationModule.OnAlertTriggered -= OnNotificationReceived;

        foreach (var bmp in _iconCache.Values)
        {
            bmp.Dispose();
        }
        _iconCache.Clear();

        _drawingSurface?.Dispose();
        _surfaceBrush.Dispose();
        _surfaceVisual.Dispose();
        _rootVisual.Dispose();
        
        _dwriteFactory.Dispose();
        _wicFactory.Dispose();
        _d2dContext.Dispose();
        _d2dDevice.Dispose();
        _d2dFactory.Dispose();
        _dxgiDevice.Dispose();
        _d3dDevice.Dispose();
    }
}
