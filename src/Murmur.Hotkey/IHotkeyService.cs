namespace Murmur.Hotkey;

/// <summary>
/// A global keyboard hook that reports raw key-down and key-up for a single configured
/// virtual-key. Push-to-talk vs toggle semantics are interpreted by the consumer, keeping
/// this service free of any dependency on application settings.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// The virtual-key code being watched (e.g. 0xA3 for Right Ctrl). May be changed while
    /// running; takes effect for subsequent key events.
    /// </summary>
    int VirtualKey { get; set; }

    /// <summary>
    /// Raised once when the hotkey transitions from up to down. Auto-repeat key-downs while
    /// the key is held are suppressed.
    /// </summary>
    event EventHandler? HotkeyDown;

    /// <summary>Raised when the hotkey is released.</summary>
    event EventHandler? HotkeyUp;

    /// <summary>Installs the low-level keyboard hook and begins reporting events.</summary>
    void Start();

    /// <summary>Removes the keyboard hook and stops reporting events.</summary>
    void Stop();
}
