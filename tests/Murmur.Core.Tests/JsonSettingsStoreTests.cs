using System.Text.Json;
using Murmur.Core.Settings;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public JsonSettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MurmurTests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var store = new JsonSettingsStore(_dir);

        var settings = store.Load();

        Assert.Equal(0xA3, settings.HotkeyVirtualKey); // Right Ctrl
        Assert.Equal(HotkeyMode.PushToTalk, settings.HotkeyMode);
        Assert.Equal("ggml-base", settings.ModelName);
        Assert.Equal(150, settings.ClipboardRestoreDelayMs);
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsValues()
    {
        var store = new JsonSettingsStore(_dir);
        var original = new MurmurSettings
        {
            HotkeyVirtualKey = 0xA4,
            HotkeyMode = HotkeyMode.Toggle,
            ModelName = "ggml-small",
            Language = "en",
            ClipboardRestoreDelayMs = 250,
            PostKeyUpDelayMs = 75,
        };

        await store.SaveAsync(original);
        var loaded = new JsonSettingsStore(_dir).Load();

        Assert.Equal(original.HotkeyVirtualKey, loaded.HotkeyVirtualKey);
        Assert.Equal(original.HotkeyMode, loaded.HotkeyMode);
        Assert.Equal(original.ModelName, loaded.ModelName);
        Assert.Equal(original.Language, loaded.Language);
        Assert.Equal(original.ClipboardRestoreDelayMs, loaded.ClipboardRestoreDelayMs);
        Assert.Equal(original.PostKeyUpDelayMs, loaded.PostKeyUpDelayMs);
    }

    [Fact]
    public async Task Save_PreservesUnknownFutureFields()
    {
        var store = new JsonSettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        // Simulate a settings file written by a newer version with an unknown field.
        await File.WriteAllTextAsync(
            store.FilePath,
            "{ \"ModelName\": \"ggml-base\", \"FutureSetting\": \"keep-me\" }");

        var loaded = store.Load();
        await store.SaveAsync(loaded);

        var json = await File.ReadAllTextAsync(store.FilePath);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("FutureSetting", out var future));
        Assert.Equal("keep-me", future.GetString());
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        var store = new JsonSettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ this is not valid json");

        var settings = store.Load();

        Assert.Equal("ggml-base", settings.ModelName);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
