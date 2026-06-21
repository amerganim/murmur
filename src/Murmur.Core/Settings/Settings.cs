using System.Text.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core.Settings;

/// <summary>
/// User-configurable settings, persisted as JSON in <c>%AppData%\Murmur\settings.json</c>.
/// All properties have sensible defaults so a fresh install works with no configuration.
/// </summary>
public sealed class MurmurSettings
{
    /// <summary>
    /// Virtual-key code of the push-to-talk / toggle hotkey.
    /// Defaults to Right Ctrl (<c>VK_RCONTROL</c> = 0xA3).
    /// </summary>
    public int HotkeyVirtualKey { get; set; } = 0xA3;

    /// <summary>How the hotkey drives recording. Defaults to push-to-talk.</summary>
    public HotkeyMode HotkeyMode { get; set; } = HotkeyMode.PushToTalk;

    /// <summary>Whether Murmur launches automatically when the user signs in. Defaults to off.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Whether the first-run wizard has completed. Used to show the wizard only once.
    /// </summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>
    /// NAudio/WASAPI capture device id, or <c>null</c> to use the system default microphone.
    /// </summary>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>Whisper GGUF model name (without extension). Defaults to <c>ggml-base</c>.</summary>
    public string ModelName { get; set; } = "ggml-base";

    /// <summary>
    /// Spoken language hint for Whisper, or <c>auto</c> for automatic detection.
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Delay before the original clipboard contents are restored after a paste, in
    /// milliseconds. Must be long enough for the target app to complete the paste;
    /// restoring synchronously corrupts the user's clipboard. Defaults to 150 ms.
    /// </summary>
    public int ClipboardRestoreDelayMs { get; set; } = 150;

    /// <summary>
    /// Delay between the hotkey being released and the paste being sent, in milliseconds.
    /// Gives the key-up event time to settle so it does not collide with injected input.
    /// Defaults to 50 ms.
    /// </summary>
    public int PostKeyUpDelayMs { get; set; } = 50;

    /// <summary>
    /// Foreground process names (without extension, case-insensitive) that should be pasted
    /// into with Ctrl+Shift+V instead of Ctrl+V, because plain Ctrl+V is reserved in
    /// terminals. VS Code (<c>Code</c>) is deliberately excluded: Ctrl+Shift+V opens the
    /// Markdown preview in its editor, so it stays on Ctrl+V; add it here if you only ever
    /// dictate into its integrated terminal.
    /// </summary>
    public List<string> TerminalProcessNames { get; set; } = new()
    {
        "WindowsTerminal",
        "WindowsTerminalPreview",
        "cmd",
        "powershell",
        "pwsh",
        "conhost",
        "OpenConsole",
        "putty",
        "mintty",
        "wezterm-gui",
        "alacritty",
    };

    /// <summary>
    /// Catches any JSON fields not mapped above so unknown/future settings written by a
    /// newer version (or hand-added by a power user) are preserved across a save instead
    /// of being silently dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}
