namespace Murmur.Core.Injection;

/// <summary>
/// Last-resort injection strategy: synthesizes each character with <c>SendInput</c> using
/// <c>KEYEVENTF_UNICODE</c>. Works in apps where clipboard paste is blocked or unsupported,
/// and never touches the clipboard. Slower for long text, and some raw-input apps (e.g. certain
/// games) ignore synthetic input — hence it sits at the end of the chain.
/// </summary>
public sealed class SendInputUnicodeInjector : ITextInjector
{
    private readonly IKeystrokeSender _keystroke;

    public SendInputUnicodeInjector(IKeystrokeSender keystroke)
    {
        _keystroke = keystroke ?? throw new ArgumentNullException(nameof(keystroke));
    }

    /// <inheritdoc />
    public string Name => "SendInputUnicode";

    /// <inheritdoc />
    public bool CanInject(string? foregroundProcessName) => true;

    /// <inheritdoc />
    public Task<InjectionResult> InjectAsync(
        string text,
        string? foregroundProcessName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(InjectionResult.Fail("No text to inject."));
        }

        try
        {
            _keystroke.SendUnicodeText(text);
            return Task.FromResult(InjectionResult.Ok);
        }
        catch (Exception ex)
        {
            return Task.FromResult(InjectionResult.Fail($"Could not synthesize keystrokes: {ex.Message}"));
        }
    }
}
