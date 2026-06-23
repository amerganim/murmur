namespace Murmur.Core.Audio;

/// <summary>
/// Lightweight audio conditioning applied before transcription. Whisper hallucinates on very
/// quiet input, so we peak-normalize the captured audio to a healthy level (and attenuate it
/// if it is hot enough to clip).
/// </summary>
public static class AudioPreprocessor
{
    /// <summary>
    /// Scales <paramref name="samples"/> so the loudest sample reaches <paramref name="targetPeak"/>.
    /// Boost is capped at <paramref name="maxGain"/> so near-silence isn't amplified into noise.
    /// Returns the input unchanged when it is effectively silent or already at the right level.
    /// </summary>
    public static float[] Normalize(float[] samples, float targetPeak = 0.95f, float maxGain = 8f)
    {
        if (samples is null || samples.Length == 0)
        {
            return samples ?? Array.Empty<float>();
        }

        var peak = 0f;
        foreach (var s in samples)
        {
            var abs = MathF.Abs(s);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        // Effectively silent — nothing meaningful to boost.
        if (peak <= 1e-4f)
        {
            return samples;
        }

        var gain = MathF.Min(targetPeak / peak, maxGain);
        if (MathF.Abs(gain - 1f) < 1e-3f)
        {
            return samples;
        }

        var result = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            result[i] = Math.Clamp(samples[i] * gain, -1f, 1f);
        }

        return result;
    }
}
