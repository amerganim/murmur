namespace Murmur.Core.Settings;

/// <summary>
/// How the global hotkey drives recording.
/// </summary>
public enum HotkeyMode
{
    /// <summary>Hold the hotkey to record; release to transcribe and inject.</summary>
    PushToTalk,

    /// <summary>Press once to start recording; press again to stop.</summary>
    Toggle,
}
