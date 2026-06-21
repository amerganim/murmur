namespace Murmur.Core.Injection;

/// <summary>
/// Which key chord sends a paste to the foreground app. Terminals typically require
/// <see cref="CtrlShiftV"/> rather than the usual <see cref="CtrlV"/>.
/// </summary>
public enum PasteChord
{
    /// <summary>Standard paste: Ctrl+V.</summary>
    CtrlV,

    /// <summary>Terminal-style paste: Ctrl+Shift+V.</summary>
    CtrlShiftV,
}
