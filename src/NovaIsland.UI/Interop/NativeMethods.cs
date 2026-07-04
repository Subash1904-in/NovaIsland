using System.Runtime.InteropServices;

namespace NovaIsland.UI.Interop;

/// <summary>
/// Source-generated P/Invoke declarations for Win32, DWM, and DXGI APIs.
/// Uses <see cref="LibraryImportAttribute"/> for Native AOT compatibility.
/// </summary>
/// <remarks>
/// All struct parameters are value types with sequential layout to ensure
/// correct marshaling without managed-heap allocations.
/// </remarks>
internal static partial class NativeMethods
{
    // ─────────────────────────────────────────────────────────────────────
    // Win32 Window Management
    // ─────────────────────────────────────────────────────────────────────

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassExW(ref WNDCLASSEXW lpWndClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport("user32.dll", EntryPoint = "UpdateWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    internal static partial int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    internal static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    internal static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", EntryPoint = "TrackMouseEvent")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindowW(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromWindow")]
    internal static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    // ─────────────────────────────────────────────────────────────────────
    // DPI
    // ─────────────────────────────────────────────────────────────────────

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("shcore.dll", EntryPoint = "GetDpiForMonitor")]
    internal static partial int GetDpiForMonitor(
        nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    // ─────────────────────────────────────────────────────────────────────
    // DWM (Desktop Window Manager)
    // ─────────────────────────────────────────────────────────────────────

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    internal static partial int DwmSetWindowAttribute(
        nint hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea")]
    internal static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

    // ─────────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────────

    // Window styles
    internal const uint WS_POPUP = 0x80000000u;
    internal const uint WS_VISIBLE = 0x10000000u;
    internal const uint WS_EX_LAYERED = 0x00080000u;
    internal const uint WS_EX_TOOLWINDOW = 0x00000080u;
    internal const uint WS_EX_NOACTIVATE = 0x08000000u;
    internal const uint WS_EX_TOPMOST = 0x00000008u;
    internal const uint WS_EX_TRANSPARENT = 0x00000020u;

    // Window messages
    internal const uint WM_DESTROY = 0x0002;
    internal const uint WM_SIZE = 0x0005;
    internal const uint WM_PAINT = 0x000F;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_QUIT = 0x0012;
    internal const uint WM_NCHITTEST = 0x0084;
    internal const uint WM_DISPLAYCHANGE = 0x007E;
    internal const uint WM_DPICHANGED = 0x02E0;
    internal const uint WM_MOUSEMOVE = 0x0200;
    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_MOUSEHOVER = 0x02A1;
    internal const uint WM_MOUSELEAVE = 0x02A3;
    internal const uint WM_USER = 0x0400;

    // TrackMouseEvent flags
    internal const uint TME_HOVER = 0x00000001;
    internal const uint TME_LEAVE = 0x00000002;

    // Custom messages
    internal const uint WM_ISLAND_TRANSITION = WM_USER + 1;

    // NCHITTEST return values
    internal const nint HTTRANSPARENT = -1;
    internal const nint HTCLIENT = 1;

    // ShowWindow commands
    internal const int SW_SHOW = 5;
    internal const int SW_HIDE = 0;

    // SetWindowPos flags
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    // SetWindowPos z-order
    internal static readonly nint HWND_TOPMOST = -1;

    // GetSystemMetrics indices
    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;

    // MonitorFromWindow flags
    internal const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    // DWM window attributes
    internal const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // DWM corner preference values
    internal const int DWMWCP_DEFAULT = 0;
    internal const int DWMWCP_DONOTROUND = 1;
    internal const int DWMWCP_ROUND = 2;
    internal const int DWMWCP_ROUNDSMALL = 3;

    // DWM system backdrop types
    internal const int DWMSBT_AUTO = 0;
    internal const int DWMSBT_NONE = 1;
    internal const int DWMSBT_MAINWINDOW = 2; // Mica
    internal const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    internal const int DWMSBT_TABBEDWINDOW = 4; // Tabbed Mica

    // ─────────────────────────────────────────────────────────────────────
    // Structs
    // ─────────────────────────────────────────────────────────────────────

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    /// <summary>Win32 MONITORINFO structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>Win32 WNDCLASSEXW structure.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
        public nint hIconSm;
    }

    /// <summary>Win32 MSG structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    /// <summary>Win32 POINT structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Win32 RECT structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>DWM MARGINS structure for frame extension.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;

        /// <summary>
        /// Creates a MARGINS that extends the frame across the entire client area.
        /// Used for Mica/Acrylic backdrop support.
        /// </summary>
        public static MARGINS EntireClientArea => new() { LeftWidth = -1, RightWidth = -1, TopHeight = -1, BottomHeight = -1 };
    }

    /// <summary>Win32 TRACKMOUSEEVENT structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public uint dwFlags;
        public nint hwndTrack;
        public uint dwHoverTime;
    }

    /// <summary>Delegate type for Win32 window procedures.</summary>
    internal delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);
}
