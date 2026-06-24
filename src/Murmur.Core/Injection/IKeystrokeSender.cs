namespace Murmur.Core.Injection;

/// <summary>
/// Synthesizes keyboard input. Abstracted so injector logic can be unit-tested without
/// sending real keystrokes.
/// </summary>
public interface IKeystrokeSender
{
    /// <summary>Sends a paste key chord to the foreground app.</summary>
    void SendPasteChord(PasteChord chord);

    /// <summary>Sends a copy (Ctrl+C) chord — used by Command Mode to grab the selection.</summary>
    void SendCopyChord();

    /// <summary>
    /// Types the given text one character at a time as Unicode key events. Used by the
    /// last-resort SendInput injector (Milestone 3).
    /// </summary>
    void SendUnicodeText(string text);
}
