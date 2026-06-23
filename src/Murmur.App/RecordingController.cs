using System.Windows.Threading;
using Murmur.App.Tray;
using Murmur.Core.Audio;
using Murmur.Core.Injection;
using Murmur.Core.Interop;
using Murmur.Core.Settings;
using Murmur.Core.Stt;
using Murmur.Hotkey;

namespace Murmur.App;

/// <summary>
/// Orchestrates the core loop: hotkey → record → transcribe → inject. Interprets push-to-talk
/// vs toggle, keeps the tray informed, and surfaces friendly errors so the app never appears
/// frozen or fails silently. All steps run on the WPF UI thread (the hotkey hook fires there),
/// so clipboard and synthesized input stay on a single thread.
/// </summary>
public sealed class RecordingController : IDisposable
{
    private enum State
    {
        Idle,
        Recording,
        Processing,
    }

    private readonly IHotkeyService _hotkey;
    private readonly IAudioCapture _capture;
    private readonly ISpeechToText _speechToText;
    private readonly TextInjectorChain _injectorChain;
    private readonly TrayController _tray;
    private readonly Func<MurmurSettings> _settings;
    private readonly Dispatcher _dispatcher;

    private State _state = State.Idle;

    public RecordingController(
        IHotkeyService hotkey,
        IAudioCapture capture,
        ISpeechToText speechToText,
        TextInjectorChain injectorChain,
        TrayController tray,
        Func<MurmurSettings> settings,
        Dispatcher dispatcher)
    {
        _hotkey = hotkey;
        _capture = capture;
        _speechToText = speechToText;
        _injectorChain = injectorChain;
        _tray = tray;
        _settings = settings;
        _dispatcher = dispatcher;

        _hotkey.HotkeyDown += OnHotkeyDown;
        _hotkey.HotkeyUp += OnHotkeyUp;
    }

    /// <summary>Begins listening for the hotkey.</summary>
    public void Start() => _hotkey.Start();

    private async void OnHotkeyDown(object? sender, EventArgs e)
    {
        try
        {
            if (_settings().HotkeyMode == HotkeyMode.Toggle)
            {
                // Toggle: down starts when idle, stops when recording.
                if (_state == State.Idle)
                {
                    await StartRecordingAsync();
                }
                else if (_state == State.Recording)
                {
                    await StopAndProcessAsync();
                }
            }
            else
            {
                // Push-to-talk: down starts recording.
                if (_state == State.Idle)
                {
                    await StartRecordingAsync();
                }
            }
        }
        catch (Exception ex)
        {
            HandleUnexpected(ex);
        }
    }

    private async void OnHotkeyUp(object? sender, EventArgs e)
    {
        try
        {
            // Push-to-talk: release stops recording. (Toggle ignores key-up.)
            if (_settings().HotkeyMode == HotkeyMode.PushToTalk && _state == State.Recording)
            {
                await StopAndProcessAsync();
            }
        }
        catch (Exception ex)
        {
            HandleUnexpected(ex);
        }
    }

    private async Task StartRecordingAsync()
    {
        _state = State.Recording;
        _tray.SetState(TrayState.Listening);

        try
        {
            await _capture.StartAsync();
        }
        catch (Exception ex)
        {
            _state = State.Idle;
            _tray.SetState(TrayState.Idle);
            _tray.ShowError("Microphone error", $"Could not start recording: {ex.Message}");
        }
    }

    private async Task StopAndProcessAsync()
    {
        _state = State.Processing;
        _tray.SetState(TrayState.Transcribing);

        try
        {
            // Let the key-up settle so it doesn't collide with injected input.
            var postKeyUp = _settings().PostKeyUpDelayMs;
            if (postKeyUp > 0)
            {
                await Task.Delay(postKeyUp);
            }

            // Capture the target app now, before we inject.
            var processName = ForegroundWindow.GetProcessName();

            var samples = await _capture.StopAsync();
            if (samples.Length == 0)
            {
                _tray.ShowInfo("Nothing recorded", "No audio was captured. Try holding the hotkey a little longer.");
                return;
            }

            if (_settings().SaveDiagnosticRecording)
            {
                TrySaveDiagnosticRecording(samples);
            }

            var text = await _speechToText.TranscribeAsync(samples);
            if (string.IsNullOrWhiteSpace(text))
            {
                _tray.ShowInfo("No speech detected", "Murmur didn't hear anything to type.");
                return;
            }

            var result = await _injectorChain.InjectAsync(text, processName);
            if (!result.Success)
            {
                _tray.ShowError("Couldn't insert text", result.FailureReason ?? "Injection failed.");
            }
        }
        finally
        {
            _state = State.Idle;
            _tray.SetState(TrayState.Idle);
        }
    }

    private static void TrySaveDiagnosticRecording(float[] samples)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Murmur");
            System.IO.Directory.CreateDirectory(dir);
            Murmur.Core.Audio.WavFile.WriteMono16k(
                System.IO.Path.Combine(dir, "last-recording.wav"), samples);
        }
        catch
        {
            // Diagnostics are best-effort; never disrupt dictation.
        }
    }

    private void HandleUnexpected(Exception ex)
    {
        _dispatcher.Invoke(() =>
        {
            _state = State.Idle;
            _tray.SetState(TrayState.Idle);
            _tray.ShowError("Murmur error", ex.Message);
        });
    }

    public void Dispose()
    {
        _hotkey.HotkeyDown -= OnHotkeyDown;
        _hotkey.HotkeyUp -= OnHotkeyUp;
        _hotkey.Dispose();
        _capture.Dispose();
        (_speechToText as IDisposable)?.Dispose();
    }
}
