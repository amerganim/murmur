namespace Murmur.App;

/// <summary>A user-facing label paired with the underlying value it selects.</summary>
public sealed record NamedOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

/// <summary>Curated hotkey choices (label → Win32 virtual-key code) for the settings dropdown.</summary>
public static class HotkeyOptions
{
    public static IReadOnlyList<NamedOption<int>> All { get; } = new[]
    {
        new NamedOption<int>("Right Ctrl", 0xA3),
        new NamedOption<int>("Left Ctrl", 0xA2),
        new NamedOption<int>("Right Alt", 0xA5),
        new NamedOption<int>("Right Shift", 0xA1),
        new NamedOption<int>("Caps Lock", 0x14),
        new NamedOption<int>("Pause/Break", 0x13),
        new NamedOption<int>("F8", 0x77),
        new NamedOption<int>("F9", 0x78),
        new NamedOption<int>("F10", 0x79),
    };

    /// <summary>Friendly name for a virtual-key, falling back to the hex code if unknown.</summary>
    public static string NameFor(int virtualKey)
    {
        foreach (var option in All)
        {
            if (option.Value == virtualKey)
            {
                return option.Label;
            }
        }

        return $"Key 0x{virtualKey:X2}";
    }
}

/// <summary>Whisper model choices (label → GGUF base name) with rough size/accuracy hints.</summary>
public static class ModelOptions
{
    public static IReadOnlyList<NamedOption<string>> All { get; } = new[]
    {
        new NamedOption<string>("Tiny — fastest, least accurate (~75 MB)", "ggml-tiny"),
        new NamedOption<string>("Base — fast, good for most uses (~145 MB)", "ggml-base"),
        new NamedOption<string>("Small — slower, more accurate (~465 MB)", "ggml-small"),
        new NamedOption<string>("Medium — slow, high accuracy (~1.5 GB)", "ggml-medium"),
        new NamedOption<string>("Large v3 — slowest, best accuracy (~3 GB)", "ggml-large-v3"),
    };
}

/// <summary>Spoken-language choices (label → Whisper language code, or <c>auto</c>).</summary>
public static class LanguageOptions
{
    public static IReadOnlyList<NamedOption<string>> All { get; } = new[]
    {
        new NamedOption<string>("Auto-detect", "auto"),
        new NamedOption<string>("English", "en"),
        new NamedOption<string>("Bengali (Bangla)", "bn"),
        new NamedOption<string>("Hindi", "hi"),
        new NamedOption<string>("Urdu", "ur"),
        new NamedOption<string>("Tamil", "ta"),
        new NamedOption<string>("Telugu", "te"),
        new NamedOption<string>("Arabic", "ar"),
        new NamedOption<string>("Spanish", "es"),
        new NamedOption<string>("French", "fr"),
        new NamedOption<string>("German", "de"),
        new NamedOption<string>("Italian", "it"),
        new NamedOption<string>("Portuguese", "pt"),
        new NamedOption<string>("Dutch", "nl"),
        new NamedOption<string>("Russian", "ru"),
        new NamedOption<string>("Ukrainian", "uk"),
        new NamedOption<string>("Turkish", "tr"),
        new NamedOption<string>("Indonesian", "id"),
        new NamedOption<string>("Vietnamese", "vi"),
        new NamedOption<string>("Thai", "th"),
        new NamedOption<string>("Chinese", "zh"),
        new NamedOption<string>("Japanese", "ja"),
        new NamedOption<string>("Korean", "ko"),
    };
}
