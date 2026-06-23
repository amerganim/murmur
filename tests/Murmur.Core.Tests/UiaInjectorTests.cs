using Murmur.Core.Injection;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class UiaInjectorTests
{
    [Fact]
    public async Task Inject_EmptyText_Fails()
    {
        var result = await new UiaInjector().InjectAsync("", foregroundProcessName: null);
        Assert.False(result.Success);
    }

    [Fact]
    public void CanInject_IsAlwaysTrue()
    {
        Assert.True(new UiaInjector().CanInject("notepad"));
    }
}
