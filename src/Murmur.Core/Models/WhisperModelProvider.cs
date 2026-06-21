namespace Murmur.Core.Models;

/// <summary>
/// Ensures Whisper GGUF models are available locally, downloading them on first run from the
/// official whisper.cpp model repository on Hugging Face. Models are cached in
/// <c>%AppData%\Murmur\models</c> (directory injectable for testing). This is the only
/// outbound network call Murmur makes by default.
/// </summary>
public sealed class WhisperModelProvider : IModelProvider
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30),
    };

    private readonly string _modelsDirectory;

    public WhisperModelProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Murmur",
            "models"))
    {
    }

    public WhisperModelProvider(string modelsDirectory)
    {
        if (string.IsNullOrWhiteSpace(modelsDirectory))
        {
            throw new ArgumentException("Models directory must be provided.", nameof(modelsDirectory));
        }

        _modelsDirectory = modelsDirectory;
    }

    /// <summary>The local path where a model with the given name would live.</summary>
    public string GetModelPath(string modelName) => Path.Combine(_modelsDirectory, modelName + ".bin");

    /// <inheritdoc />
    public async Task<string> EnsureModelAsync(
        string modelName,
        ModelDownloadProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name must be provided.", nameof(modelName));
        }

        var destination = GetModelPath(modelName);
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        Directory.CreateDirectory(_modelsDirectory);
        var temp = destination + ".download";

        try
        {
            await DownloadAsync(BaseUrl + modelName + ".bin", temp, progress, cancellationToken)
                .ConfigureAwait(false);

            File.Move(temp, destination, overwrite: true);
            return destination;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static async Task DownloadAsync(
        string url, string temp, ModelDownloadProgress? progress, CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = new FileStream(
            temp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Invoke(total.HasValue ? (double)received / total.Value : null, received, total);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
