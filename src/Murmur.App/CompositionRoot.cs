using System.IO;
using System.Windows;
using Murmur.App.Tray;
using Murmur.Core.Audio;
using Murmur.Core.Common;
using Murmur.Core.Injection;
using Murmur.Core.Models;
using Murmur.Core.Settings;
using Murmur.Core.Stt;
using Murmur.Hotkey;

namespace Murmur.App;

/// <summary>
/// Manual dependency-injection composition root. Constructs and owns all services and wires
/// them together (constructor injection throughout — no container needed), then starts the
/// record/transcribe/inject loop and ensures the Whisper model is present.
/// </summary>
public sealed class CompositionRoot : IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly MurmurSettings _settings;
    private readonly TrayController _tray;

    private RecordingController? _controller;
    private WhisperSpeechToText? _speechToText;

    /// <summary>Raised when the user requests exit (from the tray menu).</summary>
    public event EventHandler? ExitRequested;

    public CompositionRoot()
    {
        _settingsStore = new JsonSettingsStore();
        _settings = _settingsStore.Load();

        _tray = new TrayController();
        _tray.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Builds the loop, starts listening for the hotkey, and ensures the model is ready.</summary>
    public void Start()
    {
        var modelProvider = new WhisperModelProvider();
        var speechToText = new WhisperSpeechToText(
            modelProvider,
            () => _settings.ModelName,
            () => _settings.Language);
        _speechToText = speechToText;

        var capture = new NAudioWasapiCapture(_settings.MicrophoneDeviceId);

        var clipboardInjector = new ClipboardPasteInjector(
            new Win32ClipboardAccess(),
            new SendInputKeystrokeSender(),
            new TaskDelayProvider(),
            () => _settings.ClipboardRestoreDelayMs);
        var injectorChain = new TextInjectorChain(new ITextInjector[] { clipboardInjector });

        var hotkey = new LowLevelKeyboardHookService(_settings.HotkeyVirtualKey);

        _controller = new RecordingController(
            hotkey,
            capture,
            speechToText,
            injectorChain,
            _tray,
            () => _settings,
            Application.Current.Dispatcher);
        _controller.Start();

        _ = EnsureModelReadyAsync(modelProvider, speechToText);
    }

    private async Task EnsureModelReadyAsync(WhisperModelProvider modelProvider, WhisperSpeechToText speechToText)
    {
        try
        {
            var firstRun = !File.Exists(modelProvider.GetModelPath(_settings.ModelName));
            if (firstRun)
            {
                _tray.ShowInfo(
                    "Downloading speech model",
                    $"Getting the '{_settings.ModelName}' model (~150 MB). This happens once and stays on your PC.");
            }

            await modelProvider.EnsureModelAsync(_settings.ModelName).ConfigureAwait(false);
            await speechToText.WarmUpAsync().ConfigureAwait(false);

            if (firstRun)
            {
                _tray.ShowInfo("Murmur is ready", "Hold your hotkey and start speaking.");
            }
        }
        catch (Exception ex)
        {
            _tray.ShowError(
                "Speech model unavailable",
                $"Murmur couldn't prepare the model: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _speechToText?.Dispose();
        _tray.Dispose();
    }
}
