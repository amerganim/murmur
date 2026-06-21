using Murmur.Core.Common;

namespace Murmur.Core.Tests.Fakes;

/// <summary>
/// <see cref="IDelayProvider"/> whose delay does not complete until the test explicitly
/// releases it, so ordering around the clipboard-restore delay can be asserted.
/// </summary>
public sealed class ControllableDelayProvider : IDelayProvider
{
    private readonly TaskCompletionSource _gate =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int? RequestedMilliseconds { get; private set; }

    public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
    {
        RequestedMilliseconds = milliseconds;
        return _gate.Task;
    }

    /// <summary>Completes the pending delay.</summary>
    public void Release() => _gate.TrySetResult();
}

/// <summary>An <see cref="IDelayProvider"/> that completes immediately.</summary>
public sealed class ImmediateDelayProvider : IDelayProvider
{
    public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
