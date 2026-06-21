using System.Diagnostics;

namespace Murmur.Core.Interop;

/// <summary>
/// Helpers for inspecting the foreground window, used to choose injection behaviour per app.
/// </summary>
public static class ForegroundWindow
{
    /// <summary>
    /// Returns the process name (without extension) that owns the foreground window, or
    /// <c>null</c> if it cannot be determined.
    /// </summary>
    public static string? GetProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
