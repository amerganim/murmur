using Murmur.Core.Common;

namespace Murmur.Core.Injection;

/// <summary>
/// Default injection strategy: snapshot the clipboard, set it to the transcribed text, send
/// a paste chord, then restore the original clipboard contents after a short delay.
///
/// The delayed restore is critical: restoring synchronously right after sending the paste
/// chord corrupts the user's clipboard because the target app has not finished pasting yet.
/// This is the single most important behaviour in the injection layer.
/// </summary>
public sealed class ClipboardPasteInjector : ITextInjector
{
    private readonly IClipboardAccess _clipboard;
    private readonly IKeystrokeSender _keystroke;
    private readonly IDelayProvider _delay;
    private readonly Func<int> _restoreDelayMsProvider;
    private readonly Func<IReadOnlyCollection<string>> _terminalProcessNamesProvider;

    public ClipboardPasteInjector(
        IClipboardAccess clipboard,
        IKeystrokeSender keystroke,
        IDelayProvider delay,
        Func<int> restoreDelayMsProvider,
        Func<IReadOnlyCollection<string>>? terminalProcessNamesProvider = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _keystroke = keystroke ?? throw new ArgumentNullException(nameof(keystroke));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _restoreDelayMsProvider = restoreDelayMsProvider
            ?? throw new ArgumentNullException(nameof(restoreDelayMsProvider));
        _terminalProcessNamesProvider = terminalProcessNamesProvider
            ?? (static () => Array.Empty<string>());
    }

    /// <inheritdoc />
    public string Name => "ClipboardPaste";

    /// <inheritdoc />
    public bool CanInject(string? foregroundProcessName) => true;

    /// <inheritdoc />
    public async Task<InjectionResult> InjectAsync(
        string text,
        string? foregroundProcessName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return InjectionResult.Fail("No text to inject.");
        }

        // Snapshot whatever the user currently has on the clipboard so we can restore it.
        // A null snapshot means "no text" (empty, or a non-text format we will simply clear).
        string? original = TryGetClipboardText();

        try
        {
            _clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            return InjectionResult.Fail($"Could not write text to the clipboard: {ex.Message}");
        }

        var chord = ResolvePasteChord(foregroundProcessName);

        try
        {
            _keystroke.SendPasteChord(chord);
        }
        catch (Exception ex)
        {
            // The paste never landed; restore immediately so we don't leave our text behind.
            RestoreClipboard(original);
            return InjectionResult.Fail($"Could not send the paste keystroke: {ex.Message}");
        }

        // Restore on a delay — never synchronously — so the paste completes first.
        // Intentionally NOT using ConfigureAwait(false): when called from the UI thread the
        // restore should resume there so clipboard access stays on the original thread.
        try
        {
            await _delay.Delay(_restoreDelayMsProvider(), cancellationToken);
        }
        finally
        {
            RestoreClipboard(original);
        }

        return InjectionResult.Ok;
    }

    /// <summary>
    /// Chooses the paste chord for the given foreground process: terminals get Ctrl+Shift+V,
    /// everything else the standard Ctrl+V. The terminal set is configurable via settings.
    /// </summary>
    private PasteChord ResolvePasteChord(string? foregroundProcessName)
        => PasteChordResolver.Resolve(foregroundProcessName, _terminalProcessNamesProvider());

    private string? TryGetClipboardText()
    {
        try
        {
            return _clipboard.GetText();
        }
        catch
        {
            // If we cannot read the clipboard, treat it as empty rather than failing the paste.
            return null;
        }
    }

    private void RestoreClipboard(string? original)
    {
        try
        {
            if (original is null)
            {
                _clipboard.Clear();
            }
            else
            {
                _clipboard.SetText(original);
            }
        }
        catch
        {
            // Best-effort restore; never throw out of the restore path.
        }
    }
}
