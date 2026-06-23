namespace Murmur.Core.Text;

/// <summary>
/// Expands voice shortcuts: if a whole transcribed utterance matches a snippet trigger, it is
/// replaced by the snippet's expansion. Matching is whole-utterance only (case- and trailing-
/// punctuation-insensitive) so normal dictation is never altered by accident — a snippet fires
/// only when the user says exactly the trigger phrase.
/// </summary>
public static class SnippetExpander
{
    /// <summary>
    /// Returns the snippet expansion when <paramref name="text"/> matches a trigger, otherwise
    /// the original text unchanged.
    /// </summary>
    public static string Expand(string text, IReadOnlyList<Snippet>? snippets)
    {
        if (string.IsNullOrWhiteSpace(text) || snippets is null || snippets.Count == 0)
        {
            return text;
        }

        var key = Normalize(text);
        foreach (var snippet in snippets)
        {
            if (string.IsNullOrWhiteSpace(snippet.Trigger))
            {
                continue;
            }

            if (Normalize(snippet.Trigger) == key)
            {
                return snippet.Expansion;
            }
        }

        return text;
    }

    private static string Normalize(string value)
        => value.Trim().TrimEnd('.', '!', '?', ',', ';', ':').Trim().ToLowerInvariant();
}
