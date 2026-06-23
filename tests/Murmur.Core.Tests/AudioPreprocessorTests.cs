using Murmur.Core.Audio;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class AudioPreprocessorTests
{
    [Fact]
    public void Normalize_BoostsQuietAudioTowardTargetPeak()
    {
        var quiet = new[] { 0.05f, -0.05f, 0.025f };
        var result = AudioPreprocessor.Normalize(quiet, targetPeak: 0.95f, maxGain: 8f);
        var peak = Math.Max(Math.Abs(result[0]), Math.Abs(result[1]));
        Assert.True(peak > 0.3f, $"expected boosted peak, got {peak}");
    }

    [Fact]
    public void Normalize_DoesNotBoostBeyondMaxGain()
    {
        var verySoft = new[] { 0.001f, -0.001f };
        var result = AudioPreprocessor.Normalize(verySoft, targetPeak: 0.95f, maxGain: 8f);
        // Gain is capped at 8x, so 0.001 -> ~0.008, never near the target.
        Assert.True(Math.Abs(result[0]) <= 0.001f * 8f + 1e-6f);
    }

    [Fact]
    public void Normalize_AttenuatesHotAudio_NoClipping()
    {
        var hot = new[] { 1.5f, -1.8f, 0.5f };
        var result = AudioPreprocessor.Normalize(hot);
        foreach (var s in result)
        {
            Assert.InRange(s, -1f, 1f);
        }
    }

    [Fact]
    public void Normalize_LeavesSilenceUntouched()
    {
        var silence = new[] { 0f, 0f, 0f };
        var result = AudioPreprocessor.Normalize(silence);
        Assert.Equal(silence, result);
    }

    [Fact]
    public void Normalize_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(AudioPreprocessor.Normalize(Array.Empty<float>()));
        Assert.Empty(AudioPreprocessor.Normalize(null!));
    }
}
