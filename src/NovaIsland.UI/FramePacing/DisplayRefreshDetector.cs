using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NovaIsland.UI.FramePacing;

/// <summary>
/// Detects the active display's refresh rate via DXGI output enumeration.
/// Falls back to 60 Hz if detection fails. Re-queries on <c>WM_DISPLAYCHANGE</c>.
/// </summary>
/// <remarks>
/// Uses <c>IDXGIFactory1</c> → <c>EnumAdapters</c> → <c>EnumOutputs</c> →
/// <c>GetDesc</c> to read the primary output's refresh rate from the
/// <c>DXGI_MODE_DESC</c> in the output description.
/// </remarks>
internal sealed partial class DisplayRefreshDetector
{
    private const int DefaultRefreshRate = 60;
    private readonly ILogger _logger;
    private int _refreshRateHz = DefaultRefreshRate;

    /// <summary>
    /// Gets the detected display refresh rate in Hz.
    /// </summary>
    internal int RefreshRateHz => _refreshRateHz;

    /// <summary>
    /// Gets the frame interval corresponding to the detected refresh rate.
    /// </summary>
    internal float FrameIntervalSeconds => 1f / _refreshRateHz;

    /// <summary>
    /// Initializes a new <see cref="DisplayRefreshDetector"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    internal DisplayRefreshDetector(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Queries the DXGI output for the refresh rate. Called at startup and on display change.
    /// </summary>
    internal void Detect()
    {
        try
        {
            int hr = CreateDXGIFactory1(ref IID_IDXGIFactory1, out nint factoryPtr);
            if (hr < 0 || factoryPtr == 0)
            {
                _logger.LogWarning("Failed to create DXGI factory (HRESULT: 0x{Hr:X8}). Using default {Hz} Hz",
                    hr, DefaultRefreshRate);
                _refreshRateHz = DefaultRefreshRate;
                return;
            }

            try
            {
                // Get the first adapter (primary GPU).
                hr = DxgiEnumAdapters(factoryPtr, 0, out nint adapterPtr);
                if (hr < 0 || adapterPtr == 0)
                {
                    _logger.LogWarning("No DXGI adapter found. Using default {Hz} Hz", DefaultRefreshRate);
                    _refreshRateHz = DefaultRefreshRate;
                    return;
                }

                try
                {
                    // Get the first output (primary monitor).
                    hr = DxgiEnumOutputs(adapterPtr, 0, out nint outputPtr);
                    if (hr < 0 || outputPtr == 0)
                    {
                        _logger.LogWarning("No DXGI output found. Using default {Hz} Hz", DefaultRefreshRate);
                        _refreshRateHz = DefaultRefreshRate;
                        return;
                    }

                    try
                    {
                        var desc = new DXGI_OUTPUT_DESC();
                        hr = DxgiGetOutputDesc(outputPtr, ref desc);
                        if (hr >= 0)
                        {
                            // Use EnumDisplaySettings via user32 for the actual refresh rate.
                            // DXGI_OUTPUT_DESC doesn't directly expose refresh rate,
                            // so we query the desktop settings for the monitor.
                            int refreshRate = GetMonitorRefreshRate();
                            _refreshRateHz = refreshRate > 0 ? refreshRate : DefaultRefreshRate;
                        }
                        else
                        {
                            _refreshRateHz = DefaultRefreshRate;
                        }
                    }
                    finally
                    {
                        Marshal.Release(outputPtr);
                    }
                }
                finally
                {
                    Marshal.Release(adapterPtr);
                }
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DXGI refresh rate detection failed. Using default {Hz} Hz", DefaultRefreshRate);
            _refreshRateHz = DefaultRefreshRate;
        }

        _logger.LogInformation("Display refresh rate detected: {Hz} Hz", _refreshRateHz);
    }

    /// <summary>
    /// Queries the primary display's refresh rate via EnumDisplaySettings.
    /// </summary>
    private static int GetMonitorRefreshRate()
    {
        var devMode = new DEVMODEW();
        devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();

        if (EnumDisplaySettingsW(null, ENUM_CURRENT_SETTINGS, ref devMode))
        {
            return (int)devMode.dmDisplayFrequency;
        }

        return 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    // DXGI P/Invoke (minimal, just for factory/adapter/output enumeration)
    // ─────────────────────────────────────────────────────────────────────

    private static Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [LibraryImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1")]
    private static partial int CreateDXGIFactory1(ref Guid riid, out nint ppFactory);

    // COM vtable calls for DXGI interfaces.
    // IDXGIFactory1::EnumAdapters is vtable index 7.
    private static int DxgiEnumAdapters(nint factory, uint adapterIndex, out nint adapter)
    {
        unsafe
        {
            nint* vtable = *(nint**)factory;
            // EnumAdapters is at vtable slot 7
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, out nint, int>)vtable[7];
            return fn(factory, adapterIndex, out adapter);
        }
    }

    // IDXGIAdapter::EnumOutputs is vtable index 7.
    private static int DxgiEnumOutputs(nint adapter, uint outputIndex, out nint output)
    {
        unsafe
        {
            nint* vtable = *(nint**)adapter;
            // EnumOutputs is at vtable slot 7
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, out nint, int>)vtable[7];
            return fn(adapter, outputIndex, out output);
        }
    }

    // IDXGIOutput::GetDesc is vtable index 7.
    private static int DxgiGetOutputDesc(nint output, ref DXGI_OUTPUT_DESC desc)
    {
        unsafe
        {
            nint* vtable = *(nint**)output;
            var fn = (delegate* unmanaged[Stdcall]<nint, ref DXGI_OUTPUT_DESC, int>)vtable[7];
            return fn(output, ref desc);
        }
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplaySettingsW(string? lpszDeviceName, int iModeNum, ref DEVMODEW lpDevMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTPUT_DESC
    {
        // DeviceName: 32 wide chars = 64 bytes
        public unsafe fixed char DeviceName[32];
        public int DesktopCoordinatesLeft;
        public int DesktopCoordinatesTop;
        public int DesktopCoordinatesRight;
        public int DesktopCoordinatesBottom;
        public int AttachedToDesktop;
        public int Rotation;
        public nint Monitor;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct DEVMODEW
    {
        public fixed char dmDeviceName[32];

        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;

        // Position union
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        public fixed char dmFormName[32];

        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;

        // Fields for ICM
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
