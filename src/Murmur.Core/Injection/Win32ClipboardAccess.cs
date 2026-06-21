using System.Runtime.InteropServices;
using System.Text;
using Murmur.Core.Interop;

namespace Murmur.Core.Injection;

/// <summary>
/// <see cref="IClipboardAccess"/> implemented directly against the Win32 clipboard API so it
/// can live in the headless <c>Murmur.Core</c> (no WPF/WinForms dependency). Reads and writes
/// Unicode text (<c>CF_UNICODETEXT</c>), preserving emoji and other non-ASCII characters.
/// </summary>
public sealed class Win32ClipboardAccess : IClipboardAccess
{
    private const int OpenRetries = 10;
    private const int OpenRetryDelayMs = 10;

    /// <inheritdoc />
    public string? GetText()
    {
        if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT))
        {
            return null;
        }

        if (!OpenClipboardWithRetry())
        {
            return null;
        }

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var ptr = NativeMethods.GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!OpenClipboardWithRetry())
        {
            throw new InvalidOperationException("Could not open the clipboard.");
        }

        try
        {
            NativeMethods.EmptyClipboard();

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
            if (hGlobal == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Could not allocate clipboard memory.");
            }

            var target = NativeMethods.GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(hGlobal);
                throw new InvalidOperationException("Could not lock clipboard memory.");
            }

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(hGlobal);
            }

            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                // Ownership was not transferred to the system; we must free it.
                NativeMethods.GlobalFree(hGlobal);
                throw new InvalidOperationException("SetClipboardData failed.");
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (!OpenClipboardWithRetry())
        {
            return;
        }

        try
        {
            NativeMethods.EmptyClipboard();
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static bool OpenClipboardWithRetry()
    {
        // The clipboard is a shared, exclusively-locked resource; another app may briefly
        // hold it. Retry a few times before giving up.
        for (var attempt = 0; attempt < OpenRetries; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(OpenRetryDelayMs);
        }

        return false;
    }
}
