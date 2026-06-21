using Murmur.Core.Injection;
using Murmur.Core.Tests.Fakes;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class ClipboardPasteInjectorTests
{
    private static ClipboardPasteInjector Create(
        FakeClipboardAccess clipboard,
        FakeKeystrokeSender keystroke,
        Murmur.Core.Common.IDelayProvider delay,
        int restoreDelayMs = 150)
        => new(clipboard, keystroke, delay, () => restoreDelayMs);

    [Fact]
    public async Task Inject_SetsClipboardToText_AndSendsPasteChord()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();

        var result = await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("hello world", foregroundProcessName: null);

        Assert.True(result.Success);
        Assert.Contains("set:hello world", clipboard.Log);
        Assert.Single(keystroke.PasteChords);
        Assert.Equal(PasteChord.CtrlV, keystroke.PasteChords[0]);
    }

    [Fact]
    public async Task Inject_RestoresOriginalClipboard_AfterTheDelay()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();

        var result = await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("dictated text", foregroundProcessName: null);

        Assert.True(result.Success);
        Assert.Equal("original", clipboard.Current);
    }

    [Fact]
    public async Task Inject_DoesNotRestoreBeforeDelayElapses()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();
        var delay = new ControllableDelayProvider();

        var task = Create(clipboard, keystroke, delay, restoreDelayMs: 150)
            .InjectAsync("dictated text", foregroundProcessName: null);

        // The delay has not been released yet: text is on the clipboard, paste was sent,
        // but the original has NOT been restored and the call has not completed.
        Assert.False(task.IsCompleted);
        Assert.Equal("dictated text", clipboard.Current);
        Assert.Single(keystroke.PasteChords);
        Assert.Equal(150, delay.RequestedMilliseconds);

        delay.Release();
        var result = await task;

        Assert.True(result.Success);
        Assert.Equal("original", clipboard.Current);
    }

    [Fact]
    public async Task Inject_SetsClipboardBeforeSendingPaste()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();
        var clipboardValueWhenPasteSent = "<not captured>";
        keystroke.OnPasteChord = () => clipboardValueWhenPasteSent = clipboard.Current!;

        await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("payload", foregroundProcessName: null);

        Assert.Equal("payload", clipboardValueWhenPasteSent);
    }

    [Fact]
    public async Task Inject_PreservesUnicodeAndEmoji()
    {
        const string text = "café — déjà vu 🎉🚀";
        var clipboard = new FakeClipboardAccess { Current = "x" };
        var keystroke = new FakeKeystrokeSender();

        await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync(text, foregroundProcessName: null);

        Assert.Contains($"set:{text}", clipboard.Log);
    }

    [Fact]
    public async Task Inject_WhenClipboardWasEmpty_ClearsAfterRestore()
    {
        var clipboard = new FakeClipboardAccess { Current = null };
        var keystroke = new FakeKeystrokeSender();

        await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("text", foregroundProcessName: null);

        Assert.Null(clipboard.Current);
        Assert.Contains("clear", clipboard.Log);
    }

    [Fact]
    public async Task Inject_EmptyText_FailsWithoutTouchingClipboard()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();

        var result = await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("", foregroundProcessName: null);

        Assert.False(result.Success);
        Assert.Equal("original", clipboard.Current);
        Assert.Empty(keystroke.PasteChords);
    }

    [Fact]
    public async Task Inject_IntoTerminal_UsesCtrlShiftV()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender();
        var injector = new ClipboardPasteInjector(
            clipboard,
            keystroke,
            new ImmediateDelayProvider(),
            () => 150,
            () => new[] { "WindowsTerminal", "cmd" });

        await injector.InjectAsync("ls -la", foregroundProcessName: "WindowsTerminal");

        Assert.Single(keystroke.PasteChords);
        Assert.Equal(PasteChord.CtrlShiftV, keystroke.PasteChords[0]);
    }

    [Fact]
    public async Task Inject_WhenPasteSendFails_RestoresClipboardAndReportsFailure()
    {
        var clipboard = new FakeClipboardAccess { Current = "original" };
        var keystroke = new FakeKeystrokeSender { ThrowOnPaste = new InvalidOperationException("boom") };

        var result = await Create(clipboard, keystroke, new ImmediateDelayProvider())
            .InjectAsync("text", foregroundProcessName: null);

        Assert.False(result.Success);
        Assert.Equal("original", clipboard.Current);
    }
}
