using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Murmur.Core.Audio;

/// <summary>
/// Microphone capture via WASAPI (NAudio). Buffers audio between start and stop, then
/// converts it to the mono 16 kHz float format Whisper expects.
/// </summary>
public sealed class NAudioWasapiCapture : IAudioCapture
{
    private const int TargetSampleRate = 16000;

    private readonly Func<string?> _deviceIdProvider;
    private readonly object _sync = new();

    private WasapiCapture? _capture;
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

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (IsCapturing)
            {
                return Task.CompletedTask;
            }

            _capture = CreateCapture();
            _captureFormat = _capture.WaveFormat;
            _buffer = new MemoryStream();
            _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
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

            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            _capture = null;
            _buffer?.Dispose();
            _buffer = null;
        }

        return raw.Length == 0 ? Array.Empty<float>() : ConvertToMono16k(raw, format);
    }

    private WasapiCapture CreateCapture()
    {
        using var enumerator = new MMDeviceEnumerator();
        var deviceId = _deviceIdProvider();
        MMDevice device;
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                device = enumerator.GetDevice(deviceId);
            }
            catch
            {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }
        }
        else
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        return new WasapiCapture(device);
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
            _capture?.Dispose();
            _capture = null;
            _buffer?.Dispose();
            _buffer = null;
        }
    }
}
