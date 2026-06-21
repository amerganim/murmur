namespace Murmur.Core.Injection;

/// <summary>
/// A strategy for inserting transcribed text into the foreground application.
/// Implementations are tried in order by <see cref="TextInjectorChain"/> until one succeeds.
/// </summary>
public interface ITextInjector
{
    /// <summary>A short, stable name used for logging and configuration.</summary>
    string Name { get; }

    /// <summary>
    /// Whether this strategy should be attempted for the given foreground process.
    /// Lets the chain skip strategies known not to work for a particular app.
    /// </summary>
    /// <param name="foregroundProcessName">
    /// Process name of the foreground window (without extension), or <c>null</c> if unknown.
    /// </param>
    bool CanInject(string? foregroundProcessName);

    /// <summary>
    /// Attempts to inject <paramref name="text"/> into the foreground application.
    /// </summary>
    /// <param name="text">The text to insert.</param>
    /// <param name="foregroundProcessName">
    /// Process name of the foreground window (without extension), or <c>null</c> if unknown.
    /// </param>
    /// <param name="cancellationToken">Token to cancel injection.</param>
    Task<InjectionResult> InjectAsync(
        string text,
        string? foregroundProcessName,
        CancellationToken cancellationToken = default);
}
