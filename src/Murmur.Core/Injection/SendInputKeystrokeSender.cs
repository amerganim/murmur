using Murmur.Core.Interop;

namespace Murmur.Core.Injection;

/// <summary>
/// <see cref="IKeystrokeSender"/> backed by the Win32 <c>SendInput</c> API. Sends paste
/// chords and (for the Milestone 3 last-resort injector) Unicode text.
/// </summary>
public sealed class SendInputKeystrokeSender : IKeystrokeSender
{
    /// <inheritdoc />
    public void SendPasteChord(PasteChord chord)
    {
        var modifiers = chord == PasteChord.CtrlShiftV
            ? new ushort[] { NativeMethods.VK_CONTROL, NativeMethods.VK_SHIFT }
            : new ushort[] { NativeMethods.VK_CONTROL };

        var inputs = new List<NativeMethods.INPUT>();

        // Press modifiers, press V, release V, release modifiers (reverse order).
        foreach (var vk in modifiers)
        {
            inputs.Add(KeyDown(vk));
        }

        inputs.Add(KeyDown(NativeMethods.VK_V));
        inputs.Add(KeyUp(NativeMethods.VK_V));

        for (var i = modifiers.Length - 1; i >= 0; i--)
        {
            inputs.Add(KeyUp(modifiers[i]));
        }

        Send(inputs.ToArray());
    }

    /// <inheritdoc />
    public void SendUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (var ch in text)
        {
            inputs.Add(UnicodeKey(ch, keyUp: false));
            inputs.Add(UnicodeKey(ch, keyUp: true));
        }

        if (inputs.Count > 0)
        {
            Send(inputs.ToArray());
        }
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput sent {sent} of {inputs.Length} events.");
        }
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => MakeKey(vk, 0, 0);

    private static NativeMethods.INPUT KeyUp(ushort vk) => MakeKey(vk, 0, NativeMethods.KEYEVENTF_KEYUP);

    private static NativeMethods.INPUT UnicodeKey(char ch, bool keyUp)
    {
        var flags = NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u);
        return MakeKey(0, ch, flags);
    }

    private static NativeMethods.INPUT MakeKey(ushort vk, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            },
        },
    };
}
