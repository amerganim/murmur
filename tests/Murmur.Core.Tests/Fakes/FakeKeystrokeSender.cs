using Murmur.Core.Injection;

namespace Murmur.Core.Tests.Fakes;

/// <summary>Records paste chords / unicode text instead of sending real keystrokes.</summary>
public sealed class FakeKeystrokeSender : IKeystrokeSender
{
    public List<PasteChord> PasteChords { get; } = new();
    public string? LastUnicodeText { get; private set; }

    /// <summary>Optional hook invoked when a paste chord is sent (lets tests assert ordering).</summary>
    public Action? OnPasteChord { get; set; }

    /// <summary>If set, <see cref="SendPasteChord"/> throws this to simulate a send failure.</summary>
    public Exception? ThrowOnPaste { get; set; }

    public void SendPasteChord(PasteChord chord)
    {
        if (ThrowOnPaste is not null)
        {
            throw ThrowOnPaste;
        }

        PasteChords.Add(chord);
        OnPasteChord?.Invoke();
    }

    public int CopyChordCount { get; private set; }

    public void SendCopyChord() => CopyChordCount++;

    public void SendUnicodeText(string text) => LastUnicodeText = text;
}
