using System.Runtime.InteropServices;

namespace Murmur.Hotkey;

/// <summary>
/// <see cref="IHotkeyService"/> implemented with a global <c>WH_KEYBOARD_LL</c> hook. Reports
/// key-down (once, auto-repeat suppressed) and key-up for the configured virtual-key without
/// swallowing the key, so the hotkey continues to work normally in other apps.
///
/// Must be created and started on a thread that pumps Windows messages (the WPF UI thread);
/// the hook callback is invoked on that thread.
/// </summary>
public sealed class LowLevelKeyboardHookService : IHotkeyService
{
    // Field keeps the delegate alive for the lifetime of the hook (prevents GC of the callback).
    private readonly HookNativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _isKeyDown;
    private bool _disposed;

    public LowLevelKeyboardHookService(int virtualKey)
    {
        VirtualKey = virtualKey;
        _proc = HookCallback;
    }

    /// <inheritdoc />
    public int VirtualKey { get; set; }

    /// <inheritdoc />
    public event EventHandler? HotkeyDown;

    /// <inheritdoc />
    public event EventHandler? HotkeyUp;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        var moduleHandle = HookNativeMethods.GetModuleHandle(null);
        _hookId = HookNativeMethods.SetWindowsHookEx(
            HookNativeMethods.WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        HookNativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _isKeyDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<HookNativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((int)data.vkCode == VirtualKey)
            {
                var message = wParam.ToInt32();
                switch (message)
                {
                    case HookNativeMethods.WM_KEYDOWN:
                    case HookNativeMethods.WM_SYSKEYDOWN:
                        if (!_isKeyDown)
                        {
                            _isKeyDown = true;
                            HotkeyDown?.Invoke(this, EventArgs.Empty);
                        }

                        break;

                    case HookNativeMethods.WM_KEYUP:
                    case HookNativeMethods.WM_SYSKEYUP:
                        if (_isKeyDown)
                        {
                            _isKeyDown = false;
                            HotkeyUp?.Invoke(this, EventArgs.Empty);
                        }

                        break;
                }
            }
        }

        return HookNativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
