namespace Murmur.Core.Common;

/// <summary>
/// Abstraction over <see cref="Task.Delay(int, CancellationToken)"/> so time-dependent
/// logic (like the clipboard restore delay) can be controlled in unit tests.
/// </summary>
public interface IDelayProvider
{
    /// <summary>Completes after the given number of milliseconds.</summary>
    Task Delay(int milliseconds, CancellationToken cancellationToken = default);
}

/// <summary>Real <see cref="IDelayProvider"/> backed by <see cref="Task.Delay(int, CancellationToken)"/>.</summary>
public sealed class TaskDelayProvider : IDelayProvider
{
    /// <inheritdoc />
    public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
        => Task.Delay(milliseconds, cancellationToken);
}
