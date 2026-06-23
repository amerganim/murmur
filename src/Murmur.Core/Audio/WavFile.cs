using NAudio.Wave;

namespace Murmur.Core.Audio;

/// <summary>Writes captured audio to a WAV file (used for diagnostic recordings).</summary>
public static class WavFile
{
    /// <summary>Writes mono 16 kHz float samples to <paramref name="path"/> as a WAV file.</summary>
    public static void WriteMono16k(string path, float[] samples)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
        using var writer = new WaveFileWriter(path, format);
        writer.WriteSamples(samples, 0, samples.Length);
    }
}
