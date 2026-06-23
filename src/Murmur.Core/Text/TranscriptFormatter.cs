namespace Murmur.Core.Text;

/// <summary>
/// Applies per-app formatting tweaks to transcribed text before injection. Currently: strip the
/// trailing sentence punctuation Whisper tends to add when dictating into a terminal, where
/// "ls -la." would be wrong.
/// </summary>
public static class TranscriptFormatter
{
    /// <summary>
    /// Formats <paramref name="text"/> for the given foreground app. Returns it unchanged when
    /// no rule applies.
    /// </summary>
    public static string Format(
        string text,
        string? foregroundProcessName,
        IReadOnlyCollection<string>? terminalProcessNames,
        bool stripTrailingPunctuationInTerminals)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (stripTrailingPunctuationInTerminals && IsTerminal(foregroundProcessName, terminalProcessNames))
        {
            return text.TrimEnd().TrimEnd('.', '!', '?');
        }

        return text;
    }

    private static bool IsTerminal(string? processName, IReadOnlyCollection<string>? terminals)
    {
        if (string.IsNullOrEmpty(processName) || terminals is null)
        {
            return false;
        }

        foreach (var name in terminals)
        {
            if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
