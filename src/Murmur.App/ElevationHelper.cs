using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Murmur.App;

/// <summary>
/// Helpers for the optional "run as administrator" mode. A non-elevated process cannot send
/// input to an elevated window (Windows UIPI), so dictating into apps run as admin requires
/// Murmur itself to be elevated.
/// </summary>
public static class ElevationHelper
{
    /// <summary>Whether the current process is running elevated (as administrator).</summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Launches a new elevated Murmur instance (triggers a UAC prompt) and returns whether the
    /// launch was started. The new instance is passed <c>--wait-mutex</c> so it waits for this
    /// instance to exit and release the single-instance lock. Returns false if the user
    /// cancels UAC or there is no real executable (e.g. running under <c>dotnet run</c>).
    /// </summary>
    public static bool TryRestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) ||
            string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--wait-mutex",
            });
            return true;
        }
        catch (Exception)
        {
            // Win32Exception 1223 == user cancelled the UAC prompt; treat any failure as "no".
            return false;
        }
    }
}
