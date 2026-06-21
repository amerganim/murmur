using NAudio.Wave;

namespace Murmur.Core.Audio;

/// <summary>
/// Wraps an in-memory float array as an <see cref="ISampleProvider"/> so it can be fed into
/// NAudio's resampler. Mono by design.
/// </summary>
internal sealed class FloatArraySampleProvider : ISampleProvider
{
    private readonly float[] _samples;
    private int _position;

    public FloatArraySampleProvider(float[] samples, int sampleRate)
    {
        _samples = samples;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels: 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var remaining = _samples.Length - _position;
        var toCopy = Math.Min(remaining, count);
        if (toCopy <= 0)
        {
            return 0;
        }

        Array.Copy(_samples, _position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }
}
