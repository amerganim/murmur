namespace Murmur.Core.Commanding;

/// <summary>
/// Talks to a local Ollama server to rewrite selected text per a spoken instruction
/// (Command Mode). All processing is local; the only call is to the user's own machine.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Applies <paramref name="instruction"/> to <paramref name="selectedText"/> and returns the
    /// rewritten text. Throws <see cref="OllamaUnavailableException"/> if Ollama isn't reachable.
    /// </summary>
    Task<string> RewriteAsync(string instruction, string selectedText, CancellationToken cancellationToken = default);
}
