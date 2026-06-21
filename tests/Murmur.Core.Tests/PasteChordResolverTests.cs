using Murmur.Core.Injection;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class PasteChordResolverTests
{
    private static readonly string[] Terminals =
    {
        "WindowsTerminal", "cmd", "powershell", "pwsh",
    };

    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void Resolve_Terminal_UsesCtrlShiftV(string process)
        => Assert.Equal(PasteChord.CtrlShiftV, PasteChordResolver.Resolve(process, Terminals));

    [Fact]
    public void Resolve_IsCaseInsensitive()
        => Assert.Equal(PasteChord.CtrlShiftV, PasteChordResolver.Resolve("POWERSHELL", Terminals));

    [Theory]
    [InlineData("notepad")]
    [InlineData("chrome")]
    [InlineData("Code")] // VS Code editor stays on Ctrl+V (Ctrl+Shift+V opens Markdown preview).
    public void Resolve_NonTerminal_UsesCtrlV(string process)
        => Assert.Equal(PasteChord.CtrlV, PasteChordResolver.Resolve(process, Terminals));

    [Fact]
    public void Resolve_NullOrUnknownForeground_UsesCtrlV()
    {
        Assert.Equal(PasteChord.CtrlV, PasteChordResolver.Resolve(null, Terminals));
        Assert.Equal(PasteChord.CtrlV, PasteChordResolver.Resolve("cmd", null));
    }
}
