using Murmur.Core.Injection;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class TextInjectorChainTests
{
    private sealed class StubInjector : ITextInjector
    {
        public string Name { get; init; } = "stub";
        public bool Applicable { get; init; } = true;
        public bool Succeeds { get; init; }
        public Exception? Throws { get; init; }
        public int CallCount { get; private set; }

        public bool CanInject(string? foregroundProcessName) => Applicable;

        public Task<InjectionResult> InjectAsync(
            string text, string? foregroundProcessName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Throws is not null)
            {
                throw Throws;
            }

            return Task.FromResult(Succeeds ? InjectionResult.Ok : InjectionResult.Fail($"{Name} failed"));
        }
    }

    [Fact]
    public async Task Chain_StopsAtFirstSuccess()
    {
        var first = new StubInjector { Name = "first", Succeeds = false };
        var second = new StubInjector { Name = "second", Succeeds = true };
        var third = new StubInjector { Name = "third", Succeeds = true };
        var chain = new TextInjectorChain(new ITextInjector[] { first, second, third });

        var result = await chain.InjectAsync("text", null);

        Assert.True(result.Success);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(0, third.CallCount); // never reached
    }

    [Fact]
    public async Task Chain_SkipsStrategiesThatCannotInject()
    {
        var skipped = new StubInjector { Name = "skipped", Applicable = false, Succeeds = true };
        var used = new StubInjector { Name = "used", Succeeds = true };
        var chain = new TextInjectorChain(new ITextInjector[] { skipped, used });

        var result = await chain.InjectAsync("text", "someapp");

        Assert.True(result.Success);
        Assert.Equal(0, skipped.CallCount);
        Assert.Equal(1, used.CallCount);
    }

    [Fact]
    public async Task Chain_WhenAllFail_AggregatesReasons()
    {
        var first = new StubInjector { Name = "first", Succeeds = false };
        var second = new StubInjector { Name = "second", Throws = new InvalidOperationException("boom") };
        var chain = new TextInjectorChain(new ITextInjector[] { first, second });

        var result = await chain.InjectAsync("text", null);

        Assert.False(result.Success);
        Assert.Contains("first", result.FailureReason);
        Assert.Contains("second", result.FailureReason);
        Assert.Contains("boom", result.FailureReason);
    }

    [Fact]
    public async Task Chain_EmptyText_Fails()
    {
        var chain = new TextInjectorChain(new ITextInjector[] { new StubInjector { Succeeds = true } });

        var result = await chain.InjectAsync("", null);

        Assert.False(result.Success);
    }
}
