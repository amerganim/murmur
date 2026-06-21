namespace Murmur.Core.Settings;

/// <summary>
/// Loads and persists <see cref="MurmurSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads settings from disk, returning defaults (and creating the file) if none exist
    /// or the file is unreadable. Never throws for a missing/invalid file.
    /// </summary>
    MurmurSettings Load();

    /// <summary>Persists the given settings to disk.</summary>
    Task SaveAsync(MurmurSettings settings, CancellationToken cancellationToken = default);
}
