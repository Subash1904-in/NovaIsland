using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NovaIsland.UI.Animation;
using NovaIsland.UI.Interop;
using static NovaIsland.UI.Interop.NativeMethods;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Creates and manages a raw Win32 layered, tool, non-activating window
/// that serves as the island shell host. No XAML — the visual tree is
/// powered entirely by <see cref="Windows.UI.Composition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Window styles: <c>WS_POPUP | WS_VISIBLE</c> with extended styles
/// <c>WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST</c>.
/// This produces a borderless, non-activating overlay that doesn't appear in the taskbar.
/// </para>
/// <para>
/// DWM integration: Applies rounded corners via <c>DWMWA_WINDOW_CORNER_PREFERENCE</c>
/// and Mica/Acrylic backdrop via <c>DWMWA_SYSTEMBACKDROP_TYPE</c> +
/// <c>DwmExtendFrameIntoClientArea</c>.
/// </para>
/// </remarks>
internal sealed class IslandWindow : IDisposable
{
    private readonly ILogger _logger;
    private nint _hwnd;
    private WndProcDelegate? _wndProcDelegate; // Must be stored to prevent GC collection.
    private bool _disposed;

    // Callbacks for window events.
    private Action? _onDisplayChange;
    private Action<uint>? _onDpiChanged;

    /// <summary>The native window handle.</summary>
    internal nint Hwnd => _hwnd;

    /// <summary>
    /// Initializes a new <see cref="IslandWindow"/> but does not create it yet.
    /// Call <see cref="Create"/> from the STA thread that will own the message loop.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    internal IslandWindow(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sets the callback invoked when WM_DISPLAYCHANGE is received.
    /// </summary>
    internal void SetDisplayChangeCallback(Action callback) => _onDisplayChange = callback;

    /// <summary>
    /// Sets the callback invoked when WM_DPICHANGED is received.
    /// </summary>
    internal void SetDpiChangedCallback(Action<uint> callback) => _onDpiChanged = callback;

    /// <summary>
    /// Creates the Win32 window and applies DWM attributes.
    /// Must be called on the STA thread that will run the message loop.
    /// </summary>
    /// <param name="width">Initial width in physical pixels.</param>
    /// <param name="height">Initial height in physical pixels.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    internal void Create(int width, int height, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var hInstance = GetModuleHandleW(null);

        // Pin the WndProc delegate so it won't be collected.
        _wndProcDelegate = WndProc;
        var fnPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

        // Allocate unmanaged memory for the class name string.
        var className = "NovaIslandShell";
        var classNamePtr = Marshal.StringToHGlobalUni(className);

        try
        {
            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = 0,
                lpfnWndProc = fnPtr,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = 0,
                hCursor = LoadCursorW(0, 32512), // 32512 = IDC_ARROW
                hbrBackground = 0,
                lpszMenuName = 0,
                lpszClassName = classNamePtr,
                hIconSm = 0,
            };

            var atom = RegisterClassExW(ref wndClass);
            if (atom == 0)
            {
                _logger.LogError("Failed to register window class. Error: {Error}", Marshal.GetLastWin32Error());
                return;
            }

            // Remove WS_EX_LAYERED. DWM Mica backdrop and DesktopWindowTarget composition
            // do not function correctly if WS_EX_LAYERED is applied without layered attributes.
            uint exStyle = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            uint style = WS_POPUP | WS_VISIBLE;

            _hwnd = CreateWindowExW(
                exStyle, className, "Nova Island",
                style, x, y, width, height,
                0, 0, hInstance, 0);

            if (_hwnd == 0)
            {
                _logger.LogError("Failed to create window. Error: {Error}", Marshal.GetLastWin32Error());
                return;
            }

            ApplyDwmAttributes();

            ShowWindow(_hwnd, SW_SHOW);
            UpdateWindow(_hwnd);

            _logger.LogInformation("Island window created: HWND={Hwnd}, Size={Width}x{Height}, Position=({X},{Y})",
                _hwnd, width, height, x, y);
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    /// <summary>
    /// Repositions and resizes the window without changing Z-order or activation.
    /// </summary>
    /// <param name="x">New X position in physical pixels.</param>
    /// <param name="y">New Y position in physical pixels.</param>
    /// <param name="width">New width in physical pixels.</param>
    /// <param name="height">New height in physical pixels.</param>
    internal void Reposition(int x, int y, int width, int height)
    {
        if (_hwnd == 0) return;
        SetWindowPos(_hwnd, 0, x, y, width, height, SWP_NOACTIVATE | SWP_NOZORDER);
    }

    /// <summary>
    /// Runs the Win32 message loop. Blocks until WM_QUIT is received.
    /// Must be called on the STA thread.
    /// </summary>
    internal void RunMessageLoop()
    {
        while (GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(in msg);
            DispatchMessageW(in msg);
        }

        _logger.LogInformation("Island window message loop exited");
    }

    /// <summary>
    /// Posts WM_QUIT to terminate the message loop from another thread.
    /// </summary>
    internal void PostQuit()
    {
        if (_hwnd != 0)
        {
            PostMessageW(_hwnd, WM_CLOSE, 0, 0);
        }
    }

    /// <summary>
    /// Applies DWM rounded corners and Mica/Acrylic backdrop to the window.
    /// </summary>
    private void ApplyDwmAttributes()
    {
        // Disable DWM rounding to prevent drop shadows and margins
        int cornerPref = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // Disable Mica backdrop (opaque black background)
        int backdropType = DWMSBT_NONE;
        DwmSetWindowAttribute(_hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

        // Extend frame into entire client area for DWM backdrop support.
        var margins = MARGINS.EntireClientArea;
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        _logger.LogDebug("DWM attributes applied: RoundedCorners, Mica backdrop, ExtendedFrame");
    }

    /// <summary>
    /// Window procedure. Handles DPI changes, display changes, and hit-testing.
    /// The delegate is pinned to prevent GC collection — no per-message allocation.
    /// </summary>
    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_DPICHANGED:
            {
                uint newDpi = (uint)(wParam.ToInt64() & 0xFFFF);
                _onDpiChanged?.Invoke(newDpi);
                return 0;
            }
            case WM_DISPLAYCHANGE:
            {
                _onDisplayChange?.Invoke();
                return 0;
            }
            case WM_NCHITTEST:
            {
                // Return HTCLIENT to receive mouse messages, or HTTRANSPARENT to pass through.
                return HTCLIENT;
            }
            case WM_CLOSE:
            {
                DestroyWindow(hWnd);
                return 0;
            }
            case WM_DESTROY:
            {
                PostQuitMessage(0);
                return 0;
            }
            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        _wndProcDelegate = null;
    }
}
