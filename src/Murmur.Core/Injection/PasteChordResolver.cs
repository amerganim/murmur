namespace Murmur.Core.Injection;

/// <summary>
/// Decides which paste chord to send for a given foreground app. Terminals paste with
/// Ctrl+Shift+V because plain Ctrl+V is reserved there (interrupt / line editing), so
/// sending Ctrl+V into a terminal usually does nothing useful.
///
/// The terminal set is supplied by the caller (from settings) so it can be tuned per
/// machine without a code change.
/// </summary>
public static class PasteChordResolver
{
    /// <summary>
    /// Returns <see cref="PasteChord.CtrlShiftV"/> when <paramref name="foregroundProcessName"/>
    /// matches one of <paramref name="terminalProcessNames"/> (case-insensitive, extension-less),
    /// otherwise the standard <see cref="PasteChord.CtrlV"/>.
    /// </summary>
    public static PasteChord Resolve(
        string? foregroundProcessName,
        IReadOnlyCollection<string>? terminalProcessNames)
    {
        if (string.IsNullOrEmpty(foregroundProcessName) || terminalProcessNames is null)
        {
            return PasteChord.CtrlV;
        }

        foreach (var name in terminalProcessNames)
        {
            if (string.Equals(name, foregroundProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return PasteChord.CtrlShiftV;
            }
        }

        return PasteChord.CtrlV;
    }
}
