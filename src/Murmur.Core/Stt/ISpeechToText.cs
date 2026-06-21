namespace Murmur.Core.Stt;

/// <summary>
/// Transcribes audio to text using a local speech-to-text engine.
/// </summary>
public interface ISpeechToText
{
    /// <summary>
    /// Transcribes mono 16 kHz PCM float samples (range [-1, 1]) to text.
    /// Returns an empty string when no speech is detected.
    /// </summary>
    /// <param name="samples">Mono 16 kHz audio samples.</param>
    /// <param name="cancellationToken">Token to cancel transcription.</param>
    Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken = default);
}
