using System.Windows.Threading;
using Murmur.App.Tray;
using Murmur.Core.Audio;
using Murmur.Core.Commanding;
using Murmur.Core.Common;
using Murmur.Core.Injection;
using Murmur.Core.Settings;
using Murmur.Core.Stt;
using Murmur.Hotkey;

namespace Murmur.App;

/// <summary>
/// Command Mode: hold the command hotkey, speak an instruction (e.g. "make this formal"), and on
/// release Murmur copies the current selection, sends it with the instruction to a local Ollama
/// model, and pastes the rewrite back over the selection. Optional and only active when enabled.
/// Shares the mic capture and speech-to-text with the main dictation loop.
/// </summary>
public sealed class CommandModeController : IDisposable
{
    private enum State { Idle, Recording, Processing }

    private readonly IHotkeyService _hotkey;
    private readonly IAudioCapture _capture;
    private readonly ISpeechToText _speechToText;
    private readonly IOllamaClient _ollama;
    private readonly IClipboardAccess _clipboard;
    private readonly IKeystrokeSender _keystroke;
    private readonly IDelayProvider _delay;
    private readonly TrayController _tray;
    private readonly Func<MurmurSettings> _settings;
    private readonly Dispatcher _dispatcher;

    private State _state = State.Idle;

    public CommandModeController(
        IHotkeyService hotkey,
        IAudioCapture capture,
        ISpeechToText speechToText,
        IOllamaClient ollama,
        IClipboardAccess clipboard,
        IKeystrokeSender keystroke,
        IDelayProvider delay,
        TrayController tray,
        Func<MurmurSettings> settings,
        Dispatcher dispatcher)
    {
        _hotkey = hotkey;
        _capture = capture;
        _speechToText = speechToText;
        _ollama = ollama;
        _clipboard = clipboard;
        _keystroke = keystroke;
        _delay = delay;
        _tray = tray;
        _settings = settings;
        _dispatcher = dispatcher;

        _hotkey.HotkeyDown += OnDown;
        _hotkey.HotkeyUp += OnUp;
    }

    public void Start() => _hotkey.Start();

    private async void OnDown(object? sender, EventArgs e)
    {
        if (_state != State.Idle)
        {
            return;
        }

        try
        {
            _state = State.Recording;
            _tray.SetState(TrayState.Listening);
            await _capture.StartAsync();
        }
        catch (Exception ex)
        {
            _state = State.Idle;
            _tray.SetState(TrayState.Idle);
            _tray.ShowError("Microphone error", ex.Message);
        }
    }

    private async void OnUp(object? sender, EventArgs e)
    {
        if (_state != State.Recording)
        {
            return;
        }

        _state = State.Processing;
        _tray.SetState(TrayState.Transcribing);

        try
        {
            var samples = await _capture.StopAsync();
            var instruction = samples.Length == 0 ? string.Empty : await _speechToText.TranscribeAsync(samples);
            if (string.IsNullOrWhiteSpace(instruction))
            {
                _tray.ShowInfo("Command Mode", "No instruction heard. Hold the key, say what to do, then release.");
                return;
            }

            var original = TryGetClipboard();
            var selection = await CopySelectionAsync();
            if (string.IsNullOrWhiteSpace(selection))
            {
                RestoreClipboard(original);
                _tray.ShowInfo("Command Mode", "Select some text first, then hold the command key and speak.");
                return;
            }

            string rewritten;
            try
            {
                rewritten = await _ollama.RewriteAsync(instruction, selection);
            }
            catch (OllamaUnavailableException ex)
            {
                RestoreClipboard(original);
                _tray.ShowError("Command Mode needs Ollama", ex.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(rewritten))
            {
                RestoreClipboard(original);
                _tray.ShowInfo("Command Mode", "The model returned nothing. Try rephrasing the instruction.");
                return;
            }

            await PasteOverSelectionAsync(rewritten, original);
        }
        catch (Exception ex)
        {
            _dispatcher.Invoke(() => _tray.ShowError("Command Mode error", ex.Message));
        }
        finally
        {
            _state = State.Idle;
            _tray.SetState(TrayState.Idle);
        }
    }

    /// <summary>Copies the current selection to the clipboard and returns it.</summary>
    private async Task<string?> CopySelectionAsync()
    {
        _clipboard.Clear();
        _keystroke.SendCopyChord();
        await _delay.Delay(120, CancellationToken.None);
        return TryGetClipboard();
    }

    /// <summary>Pastes <paramref name="text"/> over the selection, then restores the clipboard.</summary>
    private async Task PasteOverSelectionAsync(string text, string? original)
    {
        _clipboard.SetText(text);
        _keystroke.SendPasteChord(PasteChord.CtrlV);
        await _delay.Delay(_settings().ClipboardRestoreDelayMs, CancellationToken.None);
        RestoreClipboard(original);
    }

    private string? TryGetClipboard()
    {
        try
        {
            return _clipboard.GetText();
        }
        catch
        {
            return null;
        }
    }

    private void RestoreClipboard(string? original)
    {
        try
        {
            if (string.IsNullOrEmpty(original))
            {
                _clipboard.Clear();
            }
            else
            {
                _clipboard.SetText(original);
            }
        }
        catch
        {
            // Best-effort restore.
        }
    }

    public void Dispose()
    {
        _hotkey.HotkeyDown -= OnDown;
        _hotkey.HotkeyUp -= OnUp;
        _hotkey.Dispose();
    }
}
