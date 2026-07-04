using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static NovaIsland.UI.Interop.NativeMethods;

namespace NovaIsland.UI.DpiAwareness;

/// <summary>
/// Manages per-monitor DPI awareness for the island shell.
/// Queries DPI for the current monitor, computes scale factors,
/// and repositions the window on DPI or monitor changes.
/// </summary>
/// <remarks>
/// <para>
/// Requires the application manifest to declare <c>PerMonitorV2</c> DPI awareness.
/// Without this, Windows will DPI-virtualize the window, producing blurry rendering.
/// </para>
/// <para>
/// Handles two scenarios:
/// <list type="bullet">
/// <item>WM_DPICHANGED: Monitor DPI changed (user adjusted scaling, or window moved to different-DPI monitor).</item>
/// <item>Display hot-plug: Monitor connected/disconnected, requiring reposition to primary.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class PerMonitorDpiHelper
{
    private const uint BaseDpi = 96;
    private readonly ILogger _logger;
    private uint _currentDpi = BaseDpi;
    private float _scaleFactor = 1.0f;

    /// <summary>
    /// Gets the current DPI for the island's monitor.
    /// </summary>
    internal uint CurrentDpi => _currentDpi;

    /// <summary>
    /// Gets the current scale factor (DPI / 96.0).
    /// </summary>
    internal float ScaleFactor => _scaleFactor;

    /// <summary>
    /// Initializes a new <see cref="PerMonitorDpiHelper"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    internal PerMonitorDpiHelper(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Queries the DPI for the specified window's monitor and updates the scale factor.
    /// </summary>
    /// <param name="hwnd">The window handle to query DPI for.</param>
    internal void QueryDpi(nint hwnd)
    {
        if (hwnd == 0) return;

        uint dpi = GetDpiForWindow(hwnd);
        if (dpi > 0)
        {
            UpdateDpi(dpi);
        }
    }

    /// <summary>
    /// Updates the DPI and scale factor. Called from WM_DPICHANGED handler.
    /// </summary>
    /// <param name="newDpi">The new DPI value.</param>
    internal void UpdateDpi(uint newDpi)
    {
        if (newDpi == _currentDpi) return;

        uint previousDpi = _currentDpi;
        _currentDpi = newDpi;
        _scaleFactor = (float)newDpi / BaseDpi;

        _logger.LogInformation("DPI changed: {PreviousDpi} → {NewDpi} (scale factor: {Scale:F2})",
            previousDpi, newDpi, _scaleFactor);
    }

    /// <summary>
    /// Scales a logical pixel value to physical pixels using the current DPI.
    /// </summary>
    /// <param name="logicalPixels">Value in logical (96 DPI) pixels.</param>
    /// <returns>Value in physical pixels.</returns>
    internal int ScaleToPhysical(float logicalPixels)
    {
        return (int)(logicalPixels * _scaleFactor);
    }

    /// <summary>
    /// Computes the centered position for the island on the primary monitor,
    /// accounting for DPI scaling.
    /// </summary>
    /// <param name="logicalWidth">Island width in logical pixels.</param>
    /// <param name="logicalOffsetY">Island Y offset in logical pixels.</param>
    /// <param name="x">Computed X position in physical pixels.</param>
    /// <param name="y">Computed Y position in physical pixels.</param>
    /// <param name="physicalWidth">Computed width in physical pixels.</param>
    /// <param name="physicalHeight">Computed height in physical pixels.</param>
    /// <param name="logicalHeight">Island height in logical pixels.</param>
    internal void ComputePositionCentered(
        nint hwnd, float logicalWidth, float logicalHeight, float logicalOffsetY,
        out int x, out int y, out int physicalWidth, out int physicalHeight)
    {
        physicalWidth = ScaleToPhysical(logicalWidth);
        physicalHeight = ScaleToPhysical(logicalHeight);

        nint hMonitor = 0;
        if (hwnd != 0)
        {
            hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
        }

        if (hMonitor == 0)
        {
            // Fallback if no window or monitor found
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            x = (screenWidth - physicalWidth) / 2;
            y = 0;
            return;
        }

        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfoW(hMonitor, ref mi))
        {
            int monitorWidth = mi.rcMonitor.Right - mi.rcMonitor.Left;
            x = mi.rcMonitor.Left + (monitorWidth - physicalWidth) / 2;
            y = mi.rcMonitor.Top; // Flush against top bezel, ignore logicalOffsetY
        }
        else
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            x = (screenWidth - physicalWidth) / 2;
            y = 0;
        }
    }

    /// <summary>
    /// Handles monitor hot-plug by re-querying DPI from the primary monitor
    /// and recomputing the position.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    internal void HandleDisplayChange(nint hwnd)
    {
        QueryDpi(hwnd);
        _logger.LogDebug("Display change handled. Current DPI: {Dpi}, Scale: {Scale:F2}",
            _currentDpi, _scaleFactor);
    }
}
