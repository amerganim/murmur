using Murmur.Core.Text;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class TranscriptFormatterTests
{
    private static readonly string[] Terminals = { "WindowsTerminal", "cmd", "powershell" };

    [Fact]
    public void Format_InTerminal_StripsTrailingPunctuation()
        => Assert.Equal("ls -la", TranscriptFormatter.Format("ls -la.", "WindowsTerminal", Terminals, true));

    [Fact]
    public void Format_InTerminal_WhenDisabled_LeavesText()
        => Assert.Equal("ls -la.", TranscriptFormatter.Format("ls -la.", "cmd", Terminals, false));

    [Fact]
    public void Format_InNonTerminal_LeavesText()
        => Assert.Equal("Hello there.", TranscriptFormatter.Format("Hello there.", "notepad", Terminals, true));

    [Fact]
    public void Format_NullProcess_LeavesText()
        => Assert.Equal("Hello.", TranscriptFormatter.Format("Hello.", null, Terminals, true));
}
