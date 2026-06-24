using System.IO;
using System.Windows;
using Murmur.App.Tray;
using Murmur.Core.Audio;
using Murmur.Core.Commanding;
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

    // Shared with Command Mode.
    private NAudioWasapiCapture? _capture;
    private Win32ClipboardAccess? _clipboard;
    private SendInputKeystrokeSender? _keystroke;
    private TaskDelayProvider? _delay;
    private IOllamaClient? _ollama;
    private LowLevelKeyboardHookService? _commandHotkey;
    private CommandModeController? _commandController;

    /// <summary>Raised when the user requests exit (from the tray menu).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Raised when the user asks to restart Murmur as administrator.</summary>
    public event EventHandler? RestartAsAdminRequested;

    public CompositionRoot()
    {
        _settingsStore = new JsonSettingsStore();
        _settings = _settingsStore.Load();

        _tray = new TrayController(ElevationHelper.IsElevated());
        _tray.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.SnippetsRequested += (_, _) => OpenSnippets();
        _tray.RestartAsAdminRequested += (_, _) => RestartAsAdminRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Builds the loop, starts listening for the hotkey, and ensures the model is ready.</summary>
    public void Start()
    {
        _modelProvider = new WhisperModelProvider();
        var speechToText = new WhisperSpeechToText(
            _modelProvider,
            () => _settings.ModelName,
            () => _settings.Language,
            () => _settings.TrimSilence,
            () => _settings.CustomVocabulary);
        _speechToText = speechToText;

        // Microphone, hotkey key, mode, language and delays are all read live via these
        // closures, so a settings change takes effect without rebuilding the pipeline.
        var capture = new NAudioWasapiCapture(() => _settings.MicrophoneDeviceId);
        capture.Prewarm();
        _capture = capture;

        var keystrokeSender = new SendInputKeystrokeSender();
        _keystroke = keystrokeSender;
        _clipboard = new Win32ClipboardAccess();
        _delay = new TaskDelayProvider();
        _ollama = new OllamaClient(() => _settings.OllamaEndpoint, () => _settings.OllamaModel);

        var clipboardInjector = new ClipboardPasteInjector(
            _clipboard,
            keystrokeSender,
            _delay,
            () => _settings.ClipboardRestoreDelayMs,
            () => _settings.TerminalProcessNames);

        // Fallback chain: clipboard paste (fast, at-cursor) first; if it throws, try UI
        // Automation (clean direct insert into empty fields), then synthesize the text
        // character-by-character, which works where paste is blocked.
        var injectorChain = new TextInjectorChain(new ITextInjector[]
        {
            clipboardInjector,
            new UiaInjector(),
            new SendInputUnicodeInjector(keystrokeSender),
        });

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

        UpdateCommandMode();

        if (_settings.FirstRunCompleted)
        {
            _ = EnsureModelReadyAsync();
        }
        else
        {
            RunFirstRunWizard();
        }
    }

    /// <summary>
    /// Builds, updates, or tears down the Command Mode controller to match the current settings,
    /// so toggling it on/off applies without an app restart.
    /// </summary>
    private void UpdateCommandMode()
    {
        if (_settings.CommandModeEnabled)
        {
            if (_commandController is null)
            {
                _commandHotkey = new LowLevelKeyboardHookService(_settings.CommandModeHotkeyVirtualKey);
                _commandController = new CommandModeController(
                    _commandHotkey,
                    _capture!,
                    _speechToText!,
                    _ollama!,
                    _clipboard!,
                    _keystroke!,
                    _delay!,
                    _tray,
                    () => _settings,
                    Application.Current.Dispatcher);
                _commandController.Start();
            }
            else if (_commandHotkey is not null)
            {
                _commandHotkey.VirtualKey = _settings.CommandModeHotkeyVirtualKey;
            }
        }
        else if (_commandController is not null)
        {
            _commandController.Dispose();
            _commandController = null;
            _commandHotkey = null;
        }
    }

    private void RunFirstRunWizard()
    {
        var devices = _deviceEnumerator.GetCaptureDevices();
        var wizard = new FirstRunWizard(
            _settings,
            devices,
            _modelProvider!,
            _speechToText!,
            vk =>
            {
                if (_hotkey is not null)
                {
                    _hotkey.VirtualKey = vk;
                }
            });

        wizard.ShowDialog();

        // Persist whatever the user chose and apply it, then make sure the model is ready in
        // case they closed the wizard before the test step finished preparing it.
        _ = _settingsStore.SaveAsync(_settings);
        AutoStartManager.SetEnabled(_settings.StartWithWindows);
        if (_hotkey is not null)
        {
            _hotkey.VirtualKey = _settings.HotkeyVirtualKey;
        }

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

    private void OpenSnippets()
    {
        var window = new SnippetsWindow(_settings.Snippets);
        if (window.ShowDialog() == true && window.Result is not null)
        {
            _settings.Snippets = window.Result;
            _ = _settingsStore.SaveAsync(_settings);
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

        UpdateCommandMode();

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

        var modelName = _settings.ModelName;
        var needsDownload = !File.Exists(_modelProvider.GetModelPath(modelName));
        ModelDownloadWindow? progressWindow = null;

        try
        {
            ModelDownloadProgress? progress = null;
            if (needsDownload)
            {
                var dispatcher = Application.Current.Dispatcher;
                dispatcher.Invoke(() =>
                {
                    progressWindow = new ModelDownloadWindow(modelName);
                    progressWindow.Show();
                });
                progress = (f, r, t) => progressWindow?.Report(f, r, t);
            }

            await _modelProvider.EnsureModelAsync(modelName, progress).ConfigureAwait(false);
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
        finally
        {
            if (progressWindow is not null)
            {
                Application.Current.Dispatcher.Invoke(() => progressWindow.Close());
            }
        }
    }

    public void Dispose()
    {
        _commandController?.Dispose();
        _controller?.Dispose();
        _capture?.Dispose();
        _speechToText?.Dispose();
        _tray.Dispose();
    }
}
