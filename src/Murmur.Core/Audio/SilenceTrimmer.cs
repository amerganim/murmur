namespace Murmur.Core.Audio;

/// <summary>
/// Trims leading and trailing silence from captured audio using a simple energy gate. Less
/// audio means faster transcription and fewer edge hallucinations (Whisper tends to invent
/// text over long silent stretches). Only the ends are trimmed — internal pauses are kept so
/// no spoken words are lost.
/// </summary>
public static class SilenceTrimmer
{
    private const int SampleRate = 16000;
    private const int FrameSize = SampleRate / 50; // 20 ms frames.

    /// <summary>
    /// Returns <paramref name="samples"/> with silent head/tail removed, keeping
    /// <paramref name="paddingMs"/> of context around the detected speech. Returns the input
    /// unchanged if it is silent or too short to analyse.
    /// </summary>
    public static float[] Trim(float[] samples, int paddingMs = 100)
    {
        if (samples is null || samples.Length < FrameSize * 2)
        {
            return samples ?? Array.Empty<float>();
        }

        var frameCount = samples.Length / FrameSize;
        var energies = new float[frameCount];
        var maxEnergy = 0f;
        for (var f = 0; f < frameCount; f++)
        {
            var sum = 0f;
            var start = f * FrameSize;
            for (var i = 0; i < FrameSize; i++)
            {
                var s = samples[start + i];
                sum += s * s;
            }

            var rms = MathF.Sqrt(sum / FrameSize);
            energies[f] = rms;
            if (rms > maxEnergy)
            {
                maxEnergy = rms;
            }
        }

        // Gate relative to the loudest frame, with an absolute floor so pure noise isn't "speech".
        var threshold = MathF.Max(maxEnergy * 0.10f, 0.005f);

        int firstFrame = -1, lastFrame = -1;
        for (var f = 0; f < frameCount; f++)
        {
            if (energies[f] >= threshold)
            {
                if (firstFrame < 0)
                {
                    firstFrame = f;
                }

                lastFrame = f;
            }
        }

        // No frame crossed the gate — treat as silence and leave the audio untouched.
        if (firstFrame < 0)
        {
            return samples;
        }

        var padFrames = Math.Max(0, paddingMs / 20);
        var startSample = Math.Max(0, (firstFrame - padFrames) * FrameSize);
        var endSample = Math.Min(samples.Length, (lastFrame + 1 + padFrames) * FrameSize);
        var length = endSample - startSample;

        if (length <= 0 || length >= samples.Length)
        {
            return samples;
        }

        var trimmed = new float[length];
        Array.Copy(samples, startSample, trimmed, 0, length);
        return trimmed;
    }
}
