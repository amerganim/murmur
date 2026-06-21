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
    private readonly IAudioDeviceEnumerator _deviceEnumerator = new NAudioDeviceEnumerator();

    private RecordingController? _controller;
    private WhisperSpeechToText? _speechToText;
    private WhisperModelProvider? _modelProvider;
    private LowLevelKeyboardHookService? _hotkey;
    private bool _settingsOpen;

    /// <summary>Raised when the user requests exit (from the tray menu).</summary>
    public event EventHandler? ExitRequested;

    public CompositionRoot()
    {
        _settingsStore = new JsonSettingsStore();
        _settings = _settingsStore.Load();

        _tray = new TrayController();
        _tray.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _tray.SettingsRequested += (_, _) => OpenSettings();
    }

    /// <summary>Builds the loop, starts listening for the hotkey, and ensures the model is ready.</summary>
    public void Start()
    {
        _modelProvider = new WhisperModelProvider();
        var speechToText = new WhisperSpeechToText(
            _modelProvider,
            () => _settings.ModelName,
            () => _settings.Language);
        _speechToText = speechToText;

        // Microphone, hotkey key, mode, language and delays are all read live via these
        // closures, so a settings change takes effect without rebuilding the pipeline.
        var capture = new NAudioWasapiCapture(() => _settings.MicrophoneDeviceId);

        var clipboardInjector = new ClipboardPasteInjector(
            new Win32ClipboardAccess(),
            new SendInputKeystrokeSender(),
            new TaskDelayProvider(),
            () => _settings.ClipboardRestoreDelayMs,
            () => _settings.TerminalProcessNames);
        var injectorChain = new TextInjectorChain(new ITextInjector[] { clipboardInjector });

        _hotkey = new LowLevelKeyboardHookService(_settings.HotkeyVirtualKey);

        _controller = new RecordingController(
            _hotkey,
            capture,
            speechToText,
            injectorChain,
            _tray,
            () => _settings,
            Application.Current.Dispatcher);
        _controller.Start();

        _ = EnsureModelReadyAsync();
    }

    private void OpenSettings()
    {
        // Guard against opening multiple settings windows from repeated tray clicks.
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        try
        {
            var devices = _deviceEnumerator.GetCaptureDevices();
            var window = new SettingsWindow(_settings, devices);
            var saved = window.ShowDialog() == true;
            if (saved)
            {
                ApplySettings(window.ModelChanged);
            }
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void ApplySettings(bool modelChanged)
    {
        _ = _settingsStore.SaveAsync(_settings);

        if (!AutoStartManager.SetEnabled(_settings.StartWithWindows) && _settings.StartWithWindows)
        {
            _tray.ShowError("Couldn't enable auto-start", "Murmur could not update the startup setting.");
        }

        // Hotkey, mic, mode, language and delays apply live; only the model needs a reload.
        if (_hotkey is not null)
        {
            _hotkey.VirtualKey = _settings.HotkeyVirtualKey;
        }

        if (modelChanged && _speechToText is not null)
        {
            _ = ReloadModelAsync();
        }
    }

    private async Task ReloadModelAsync()
    {
        if (_speechToText is null)
        {
            return;
        }

        await _speechToText.ResetAsync().ConfigureAwait(false);
        await EnsureModelReadyAsync().ConfigureAwait(false);
    }

    private async Task EnsureModelReadyAsync()
    {
        if (_modelProvider is null || _speechToText is null)
        {
            return;
        }

        try
        {
            var needsDownload = !File.Exists(_modelProvider.GetModelPath(_settings.ModelName));
            if (needsDownload)
            {
                _tray.ShowInfo(
                    "Downloading speech model",
                    $"Getting the '{_settings.ModelName}' model. This stays on your PC and only downloads once.");
            }

            await _modelProvider.EnsureModelAsync(_settings.ModelName).ConfigureAwait(false);
            await _speechToText.WarmUpAsync().ConfigureAwait(false);

            if (needsDownload)
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
