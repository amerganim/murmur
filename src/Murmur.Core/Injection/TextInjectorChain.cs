namespace Murmur.Core.Injection;

/// <summary>
/// Tries a sequence of <see cref="ITextInjector"/> strategies in order until one succeeds.
/// Strategies that report they cannot handle the foreground app are skipped. If every
/// applicable strategy fails, the aggregated failure reasons are returned.
/// </summary>
public sealed class TextInjectorChain
{
    private readonly IReadOnlyList<ITextInjector> _injectors;

    public TextInjectorChain(IEnumerable<ITextInjector> injectors)
    {
        ArgumentNullException.ThrowIfNull(injectors);
        _injectors = injectors.ToList();
    }

    /// <summary>
    /// Attempts to inject <paramref name="text"/> using each applicable strategy in turn.
    /// </summary>
    public async Task<InjectionResult> InjectAsync(
        string text,
        string? foregroundProcessName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return InjectionResult.Fail("No text to inject.");
        }

        var failures = new List<string>();

        foreach (var injector in _injectors)
        {
            if (!injector.CanInject(foregroundProcessName))
            {
                continue;
            }

            InjectionResult result;
            try
            {
                result = await injector.InjectAsync(text, foregroundProcessName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = InjectionResult.Fail($"{injector.Name} threw: {ex.Message}");
            }

            if (result.Success)
            {
                return result;
            }

            failures.Add($"{injector.Name}: {result.FailureReason}");
        }

        return InjectionResult.Fail(
            failures.Count == 0
                ? "No injection strategy could handle the foreground application."
                : string.Join("; ", failures));
    }
}
