using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Murmur.App;

/// <summary>
/// Manages "start Murmur when I sign in" via the per-user Run registry key. Per-user (HKCU)
/// needs no elevation, so the toggle works without admin rights.
/// </summary>
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Murmur";

    /// <summary>Whether Murmur is currently registered to start at sign-in.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry. Returns whether the change succeeded.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                var exe = GetExecutablePath();
                if (exe is null)
                {
                    return false;
                }

                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Path to the running Murmur executable. Returns <c>null</c> under <c>dotnet run</c>
    /// (where the host is dotnet.exe, not a real Murmur exe) so we never register that.
    /// </summary>
    private static string? GetExecutablePath()
    {
        var path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        return string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase) ? null : path;
    }
}
