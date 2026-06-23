using Murmur.Core.Audio;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class SilenceTrimmerTests
{
    private const int SampleRate = 16000;

    private static float[] Tone(int samples, float amp = 0.5f)
    {
        var data = new float[samples];
        for (var i = 0; i < samples; i++)
        {
            data[i] = amp * MathF.Sin(2f * MathF.PI * 200f * i / SampleRate);
        }

        return data;
    }

    [Fact]
    public void Trim_RemovesLeadingAndTrailingSilence()
    {
        var silence = new float[SampleRate]; // 1s of silence each side
        var speech = Tone(SampleRate);       // 1s of tone
        var input = new float[silence.Length + speech.Length + silence.Length];
        Array.Copy(speech, 0, input, silence.Length, speech.Length);

        var result = SilenceTrimmer.Trim(input, paddingMs: 100);

        // Should be much shorter than the 3s input, but still contain the ~1s of speech.
        Assert.True(result.Length < input.Length);
        Assert.True(result.Length >= SampleRate);
    }

    [Fact]
    public void Trim_AllSilence_ReturnsInputUnchanged()
    {
        var silence = new float[SampleRate];
        var result = SilenceTrimmer.Trim(silence);
        Assert.Equal(silence.Length, result.Length);
    }

    [Fact]
    public void Trim_TooShort_ReturnsInput()
    {
        var tiny = new float[100];
        Assert.Equal(tiny, SilenceTrimmer.Trim(tiny));
    }

    [Fact]
    public void Trim_KeepsInternalPause()
    {
        // speech | 0.5s silence | speech  — the internal pause must be preserved.
        var speech = Tone(SampleRate / 2);
        var pause = new float[SampleRate / 2];
        var input = new float[speech.Length + pause.Length + speech.Length];
        Array.Copy(speech, 0, input, 0, speech.Length);
        Array.Copy(speech, 0, input, speech.Length + pause.Length, speech.Length);

        var result = SilenceTrimmer.Trim(input, paddingMs: 0);

        // Whole span is speech-to-speech, so nothing is trimmed away internally.
        Assert.True(result.Length >= speech.Length + pause.Length + speech.Length - SampleRate / 50);
    }
}
