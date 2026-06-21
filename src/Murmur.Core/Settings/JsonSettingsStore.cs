using System.Text.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core.Settings;

/// <summary>
/// <see cref="ISettingsStore"/> backed by a human-readable JSON file. By default this lives
/// at <c>%AppData%\Murmur\settings.json</c>; the base directory is injectable for testing.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <summary>
    /// Creates a store rooted at the default location (<c>%AppData%\Murmur</c>).
    /// </summary>
    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Murmur"))
    {
    }

    /// <summary>
    /// Creates a store rooted at <paramref name="baseDirectory"/>. Used by tests to avoid
    /// touching the real <c>%AppData%</c>.
    /// </summary>
    public JsonSettingsStore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory must be provided.", nameof(baseDirectory));
        }

        _filePath = Path.Combine(baseDirectory, "settings.json");
    }

    /// <summary>The resolved path of the settings file.</summary>
    public string FilePath => _filePath;

    /// <inheritdoc />
    public MurmurSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = new MurmurSettings();
            TrySave(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<MurmurSettings>(json, SerializerOptions) ?? new MurmurSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: fall back to defaults rather than crashing.
            return new MurmurSettings();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(MurmurSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    private void TrySave(MurmurSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: if we cannot write defaults (e.g. permissions), continue in memory.
        }
    }
}
