namespace Murmur.Core.Text;

/// <summary>
/// A voice shortcut: when the user dictates exactly the <see cref="Trigger"/> phrase, Murmur
/// types the <see cref="Expansion"/> instead (e.g. "my email" → the actual address).
/// </summary>
public sealed class Snippet
{
    /// <summary>The spoken phrase that triggers the expansion.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>The text that replaces the trigger.</summary>
    public string Expansion { get; set; } = string.Empty;
}
