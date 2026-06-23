using System.Text;
using Murmur.Core.Models;
using Whisper.net;

namespace Murmur.Core.Stt;

/// <summary>
/// Local speech-to-text using Whisper.net (whisper.cpp). The model is loaded once and kept
/// warm in memory so only the first transcription pays the load cost. A fresh processor is
/// created per transcription (cheap relative to model load).
/// </summary>
public sealed class WhisperSpeechToText : ISpeechToText, IDisposable
{
    private readonly IModelProvider _modelProvider;
    private readonly Func<string> _modelNameProvider;
    private readonly Func<string> _languageProvider;
    private readonly Func<bool> _trimSilenceProvider;
    private readonly Func<string> _promptProvider;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private WhisperFactory? _factory;
    private bool _disposed;

    public WhisperSpeechToText(
        IModelProvider modelProvider,
        Func<string> modelNameProvider,
        Func<string> languageProvider,
        Func<bool>? trimSilenceProvider = null,
        Func<string>? promptProvider = null)
    {
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
        _modelNameProvider = modelNameProvider ?? throw new ArgumentNullException(nameof(modelNameProvider));
        _languageProvider = languageProvider ?? throw new ArgumentNullException(nameof(languageProvider));
        _trimSilenceProvider = trimSilenceProvider ?? (static () => true);
        _promptProvider = promptProvider ?? (static () => string.Empty);
    }

    /// <summary>
    /// Loads the model into memory ahead of time so the first real transcription is fast.
    /// Safe to call more than once.
    /// </summary>
    public Task WarmUpAsync(CancellationToken cancellationToken = default)
        => EnsureFactoryAsync(cancellationToken);

    /// <summary>
    /// Unloads the cached model so the next warm-up / transcription reloads using the current
    /// model name. Call after the user switches models in settings.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _factory?.Dispose();
            _factory = null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken = default)
    {
        if (samples is null || samples.Length == 0)
        {
            return string.Empty;
        }

        // Trim silent head/tail (faster, fewer hallucinations), then boost quiet input —
        // Whisper hallucinates on near-silent audio.
        if (_trimSilenceProvider())
        {
            samples = Audio.SilenceTrimmer.Trim(samples);
        }

        samples = Audio.AudioPreprocessor.Normalize(samples);

        var factory = await EnsureFactoryAsync(cancellationToken).ConfigureAwait(false);
        var language = _languageProvider();

        // "auto" (or unset) → let Whisper detect the spoken language and transcribe in it;
        // otherwise force the chosen language. Transcribe (not translate), so the output is in
        // the same language that was spoken.
        var processorBuilder = factory.CreateBuilder();
        processorBuilder = string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? processorBuilder.WithLanguageDetection()
            : processorBuilder.WithLanguage(language);

        // Bias transcription toward the user's custom words/names/jargon.
        var prompt = _promptProvider();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            processorBuilder = processorBuilder.WithPrompt(prompt);
        }

        await using var processor = processorBuilder.Build();

        var text = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            text.Append(segment.Text);
        }

        return text.ToString().Trim();
    }

    private async Task<WhisperFactory> EnsureFactoryAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_factory is not null)
        {
            return _factory;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_factory is null)
            {
                var modelPath = await _modelProvider
                    .EnsureModelAsync(_modelNameProvider(), progress: null, cancellationToken)
                    .ConfigureAwait(false);
                _factory = WhisperFactory.FromPath(modelPath);
            }

            return _factory;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _factory?.Dispose();
        _initLock.Dispose();
    }
}
