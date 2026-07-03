using System.Runtime.InteropServices;

namespace NovaIsland.Infrastructure.Stability;

/// <summary>
/// Native Win32 P/Invoke declarations for stability infrastructure.
/// Uses <see cref="LibraryImportAttribute"/> for Native AOT compatibility.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Sets the minimum and maximum working set size for the specified process.
    /// Passing <c>-1</c> for both parameters trims the working set to its minimum.
    /// </summary>
    /// <param name="hProcess">A handle to the process.</param>
    /// <param name="dwMinimumWorkingSetSize">Minimum working set size in bytes, or <c>-1</c> to trim.</param>
    /// <param name="dwMaximumWorkingSetSize">Maximum working set size in bytes, or <c>-1</c> to trim.</param>
    /// <returns><c>true</c> if the function succeeds; otherwise, <c>false</c>.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessWorkingSetSize(
        nint hProcess,
        nint dwMinimumWorkingSetSize,
        nint dwMaximumWorkingSetSize);
}
