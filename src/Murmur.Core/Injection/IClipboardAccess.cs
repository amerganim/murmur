namespace Murmur.Core.Injection;

/// <summary>
/// Minimal clipboard abstraction so the paste injector's snapshot/restore logic can be
/// unit-tested without touching the real Windows clipboard.
/// </summary>
public interface IClipboardAccess
{
    /// <summary>
    /// Returns the current clipboard text, or <c>null</c> if the clipboard holds no text
    /// (e.g. it is empty or contains an image/file list).
    /// </summary>
    string? GetText();

    /// <summary>Replaces the clipboard contents with the given Unicode text.</summary>
    void SetText(string text);

    /// <summary>Clears the clipboard.</summary>
    void Clear();
}
