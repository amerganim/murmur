namespace Murmur.Core.Commanding;

/// <summary>
/// Thrown when the local Ollama server can't be reached (not installed or not running), so the
/// app can show a friendly "start Ollama" message rather than a raw network error.
/// </summary>
public sealed class OllamaUnavailableException : Exception
{
    public OllamaUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
