using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace NovaIsland.UI.Interop;

/// <summary>
/// Bridges a Win32 HWND to the Windows.UI.Composition visual tree
/// using <see cref="ICompositorDesktopInterop"/> to create a
/// <see cref="DesktopWindowTarget"/> bound to the native window.
/// </summary>
/// <remarks>
/// This is the critical link that allows a raw Win32 window to host
/// a GPU-accelerated Composition visual tree without any XAML dependency.
/// </remarks>
internal static class CompositionInterop
{
    private static nint _dispatcherQueueController;

    internal static void EnsureDispatcherQueue()
    {
        if (_dispatcherQueueController != 0) return;

        var options = new DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = 2, // DQTYPE_THREAD_CURRENT
            apartmentType = 0 // DQTAT_COM_NONE
        };

        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out _dispatcherQueueController));
    }

    /// <summary>
    /// Creates a <see cref="Compositor"/> and binds it to the specified HWND,
    /// producing a <see cref="DesktopWindowTarget"/> that roots the visual tree.
    /// </summary>
    /// <param name="hwnd">The native window handle.</param>
    /// <param name="compositor">The created Compositor instance.</param>
    /// <param name="target">The DesktopWindowTarget bound to the HWND.</param>
    /// <param name="isTopmost">Whether the target should be topmost.</param>
    internal static void CreateDesktopWindowTarget(
        nint hwnd,
        out Compositor compositor,
        out DesktopWindowTarget target,
        bool isTopmost = false)
    {
        EnsureDispatcherQueue();

        compositor = new Compositor();

        // Get the ICompositorDesktopInterop interface from the Compositor using CsWinRT.
        var interop = compositor.As<ICompositorDesktopInterop>();

        // Create the DesktopWindowTarget bound to our HWND by receiving an IntPtr first.
        interop.CreateDesktopWindowTarget(hwnd, isTopmost, out nint targetPtr);
        
        target = MarshalInterface<DesktopWindowTarget>.FromAbi(targetPtr);
    }

    [DllImport("coremessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    /// <summary>
    /// COM interface for creating DesktopWindowTarget from a Compositor.
    /// This is the WinRT interop interface exposed by the Compositor.
    /// </summary>
    [ComImport]
    [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(
            nint hwndTarget,
            [MarshalAs(UnmanagedType.Bool)] bool isTopmost,
            out nint result);
    }
}
