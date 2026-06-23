using Murmur.Core.Injection;
using Murmur.Core.Tests.Fakes;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class SendInputUnicodeInjectorTests
{
    [Fact]
    public async Task Inject_SendsTheTextAsUnicode_PreservingEmoji()
    {
        const string text = "héllo — 🎉";
        var keystroke = new FakeKeystrokeSender();

        var result = await new SendInputUnicodeInjector(keystroke)
            .InjectAsync(text, foregroundProcessName: null);

        Assert.True(result.Success);
        Assert.Equal(text, keystroke.LastUnicodeText);
    }

    [Fact]
    public async Task Inject_EmptyText_Fails()
    {
        var keystroke = new FakeKeystrokeSender();
        var result = await new SendInputUnicodeInjector(keystroke)
            .InjectAsync("", foregroundProcessName: null);

        Assert.False(result.Success);
        Assert.Null(keystroke.LastUnicodeText);
    }

    [Fact]
    public async Task Inject_DoesNotTouchClipboardOrPaste()
    {
        var keystroke = new FakeKeystrokeSender();
        await new SendInputUnicodeInjector(keystroke).InjectAsync("hi", foregroundProcessName: null);

        Assert.Empty(keystroke.PasteChords);
    }
}
