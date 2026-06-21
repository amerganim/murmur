namespace Murmur.Core.Models;

/// <summary>
/// Reports download progress for a Whisper model as a fraction in the range [0, 1],
/// or <c>null</c> when the total size is unknown.
/// </summary>
public delegate void ModelDownloadProgress(double? fractionComplete, long bytesReceived, long? totalBytes);

/// <summary>
/// Ensures the requested Whisper model is available on disk, downloading it on first run.
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// Returns the local path to the given model, downloading it if it is not already
    /// present. The returned file is guaranteed to exist on success.
    /// </summary>
    /// <param name="modelName">Model name without extension, e.g. <c>ggml-base</c>.</param>
    /// <param name="progress">Optional callback invoked as the model downloads.</param>
    /// <param name="cancellationToken">Token to cancel the download.</param>
    Task<string> EnsureModelAsync(
        string modelName,
        ModelDownloadProgress? progress = null,
        CancellationToken cancellationToken = default);
}
