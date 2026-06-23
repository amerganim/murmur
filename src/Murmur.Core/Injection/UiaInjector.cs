using FlaUI.UIA3;

namespace Murmur.Core.Injection;

/// <summary>
/// Injection via UI Automation: writes text directly into the focused control using its
/// <c>ValuePattern</c> — the cleanest insertion (no clipboard, no synthetic keystrokes).
///
/// Because <c>ValuePattern.SetValue</c> replaces the entire control contents, this strategy
/// only acts when the focused field is <b>empty</b>; otherwise it declines so the chain falls
/// through, ensuring it can never overwrite text the user already typed (accuracy first).
/// Many rich editors (browsers, Electron) don't support ValuePattern, so it can't be the only
/// method.
/// </summary>
public sealed class UiaInjector : ITextInjector
{
    /// <inheritdoc />
    public string Name => "UIA";

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
            using var automation = new UIA3Automation();
            var focused = automation.FocusedElement();
            if (focused is null)
            {
                return Fail("No focused UI element.");
            }

            var valuePattern = focused.Patterns.Value.PatternOrDefault;
            if (valuePattern is null)
            {
                return Fail("Focused control does not support direct value setting.");
            }

            if (valuePattern.IsReadOnly.ValueOrDefault)
            {
                return Fail("Focused control is read-only.");
            }

            var current = valuePattern.Value.ValueOrDefault ?? string.Empty;
            if (!string.IsNullOrEmpty(current))
            {
                // SetValue would replace existing content — decline rather than destroy text.
                return Fail("Focused field is not empty; deferring to another strategy.");
            }

            valuePattern.SetValue(text);
            return Task.FromResult(InjectionResult.Ok);
        }
        catch (Exception ex)
        {
            return Fail($"UI Automation injection failed: {ex.Message}");
        }

        static Task<InjectionResult> Fail(string reason) => Task.FromResult(InjectionResult.Fail(reason));
    }
}
