using Murmur.App.Tray;
using Murmur.Core.Settings;

namespace Murmur.App;

/// <summary>
/// Manual dependency-injection composition root. Constructs and owns all services and wires
/// them together (constructor injection throughout — no container needed). Grows milestone
/// by milestone; in Milestone 0 it loads settings and shows the tray icon.
/// </summary>
public sealed class CompositionRoot : IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly MurmurSettings _settings;
    private readonly TrayController _tray;

    /// <summary>Raised when the user requests exit (from the tray menu).</summary>
    public event EventHandler? ExitRequested;

    public CompositionRoot()
    {
        _settingsStore = new JsonSettingsStore();
        _settings = _settingsStore.Load();

        _tray = new TrayController();
        _tray.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts background services. Milestone 1 wires the record/transcribe/inject loop here.</summary>
    public void Start()
    {
        // Milestone 1: start the hotkey service and ensure the Whisper model is present.
    }

    public void Dispose()
    {
        _tray.Dispose();
    }
}
