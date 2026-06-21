namespace Murmur.App.Tray;

/// <summary>
/// Visual state shown in the system tray so the user always knows what Murmur is doing.
/// </summary>
public enum TrayState
{
    /// <summary>Idle and ready; waiting for the hotkey.</summary>
    Idle,

    /// <summary>Recording the microphone.</summary>
    Listening,

    /// <summary>Transcribing captured audio.</summary>
    Transcribing,
}
