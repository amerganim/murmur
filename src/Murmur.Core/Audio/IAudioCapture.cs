namespace Murmur.Core.Audio;

/// <summary>
/// Captures microphone audio between a start and stop, returning samples ready for Whisper.
/// </summary>
public interface IAudioCapture : IDisposable
{
    /// <summary>Whether a capture session is currently running.</summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Starts capturing from the configured (or default) microphone. Samples are buffered
    /// until <see cref="StopAsync"/> is called.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops capturing and returns the recorded audio as mono 16 kHz PCM floats in the
    /// range [-1, 1] — the format Whisper expects. Returns an empty array if nothing was
    /// captured.
    /// </summary>
    Task<float[]> StopAsync(CancellationToken cancellationToken = default);
}
