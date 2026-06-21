using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Murmur.Core.Audio;

/// <summary>
/// Microphone capture via WASAPI (NAudio). Buffers audio between start and stop, then
/// converts it to the mono 16 kHz float format Whisper expects.
///
/// The underlying <see cref="WasapiCapture"/> is created once and reused across takes (only
/// recording is started/stopped), so the audio client is not re-initialised on every hotkey
/// press — recording begins fast enough to catch the first word. The mic is still only active
/// while actually recording. It is rebuilt automatically if the selected device changes.
/// </summary>
public sealed class NAudioWasapiCapture : IAudioCapture
{
    private const int TargetSampleRate = 16000;

    private readonly Func<string?> _deviceIdProvider;
    private readonly object _sync = new();

    private WasapiCapture? _capture;
    private string? _boundDeviceId;
    private MemoryStream? _buffer;
    private WaveFormat? _captureFormat;
    private TaskCompletionSource<bool>? _stopped;

    public NAudioWasapiCapture(string? deviceId = null)
        : this(() => deviceId)
    {
    }

    /// <summary>
    /// Creates a capture whose device is resolved at each recording start via
    /// <paramref name="deviceIdProvider"/>, so a settings change takes effect on the next take
    /// without rebuilding the capture.
    /// </summary>
    public NAudioWasapiCapture(Func<string?> deviceIdProvider)
    {
        _deviceIdProvider = deviceIdProvider ?? throw new ArgumentNullException(nameof(deviceIdProvider));
    }

    /// <inheritdoc />
    public bool IsCapturing { get; private set; }

    /// <summary>
    /// Resolves and creates the capture device ahead of the first hotkey press so that the
    /// first recording starts quickly. Safe to call at startup; never throws.
    /// </summary>
    public void Prewarm()
    {
        lock (_sync)
        {
            if (IsCapturing)
            {
                return;
            }

            try
            {
                EnsureCaptureLocked();
            }
            catch
            {
                // Best-effort warm-up; a real failure will surface on the first StartAsync.
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (IsCapturing)
            {
                return Task.CompletedTask;
            }

            EnsureCaptureLocked();
            _buffer = new MemoryStream();
            _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            StartRecordingWithRetryLocked();
            IsCapturing = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<float[]> StopAsync(CancellationToken cancellationToken = default)
    {
        WasapiCapture? capture;
        Task? stopped;

        lock (_sync)
        {
            if (!IsCapturing || _capture is null)
            {
                return Array.Empty<float>();
            }

            capture = _capture;
            stopped = _stopped?.Task;
            IsCapturing = false;
        }

        capture.StopRecording();
        if (stopped is not null)
        {
            await stopped.ConfigureAwait(false);
        }

        byte[] raw;
        WaveFormat format;
        lock (_sync)
        {
            raw = _buffer?.ToArray() ?? Array.Empty<byte>();
            format = _captureFormat!;

            // Keep the capture instance (and its initialised audio client) for the next take;
            // only the per-take buffer is released here.
            _buffer?.Dispose();
            _buffer = null;
        }

        return raw.Length == 0 ? Array.Empty<float>() : ConvertToMono16k(raw, format);
    }

    /// <summary>Creates the reusable capture for the current device, rebuilding it if the device changed.</summary>
    private void EnsureCaptureLocked()
    {
        var desiredId = _deviceIdProvider();
        if (_capture is not null && _boundDeviceId == desiredId)
        {
            return;
        }

        DisposeCaptureLocked();

        var device = ResolveDevice(desiredId);
        var capture = new WasapiCapture(device);
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;

        _capture = capture;
        _captureFormat = capture.WaveFormat;
        _boundDeviceId = desiredId;
    }

    private void StartRecordingWithRetryLocked()
    {
        try
        {
            _capture!.StartRecording();
        }
        catch
        {
            // Reusing an instance can fail if the device was lost; rebuild once and retry.
            DisposeCaptureLocked();
            EnsureCaptureLocked();
            _capture!.StartRecording();
        }
    }

    private void DisposeCaptureLocked()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
        _boundDeviceId = null;
    }

    private static MMDevice ResolveDevice(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                return enumerator.GetDevice(deviceId);
            }
            catch
            {
                // Fall through to the default device if the saved id is no longer valid.
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        => _stopped?.TrySetResult(true);

    /// <summary>
    /// Converts captured raw bytes (in <paramref name="format"/>) to mono 16 kHz float samples.
    /// </summary>
    private static float[] ConvertToMono16k(byte[] raw, WaveFormat format)
    {
        using var ms = new MemoryStream(raw);
        var reader = new RawSourceWaveStream(ms, format);
        var source = reader.ToSampleProvider();

        // Read all interleaved float samples.
        var interleaved = ReadAll(source);
        var channels = format.Channels;

        // Mix down to mono by averaging channels.
        float[] mono;
        if (channels <= 1)
        {
            mono = interleaved;
        }
        else
        {
            mono = new float[interleaved.Length / channels];
            for (var i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    sum += interleaved[(i * channels) + c];
                }

                mono[i] = sum / channels;
            }
        }

        if (format.SampleRate == TargetSampleRate)
        {
            return mono;
        }

        var resampler = new WdlResamplingSampleProvider(
            new FloatArraySampleProvider(mono, format.SampleRate), TargetSampleRate);
        return ReadAll(resampler);
    }

    private static float[] ReadAll(ISampleProvider provider)
    {
        var result = new List<float>();
        var buffer = new float[16384];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            result.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        return result.ToArray();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeCaptureLocked();
            _buffer?.Dispose();
            _buffer = null;
        }
    }
}
