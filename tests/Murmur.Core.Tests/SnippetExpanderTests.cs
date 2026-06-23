using Murmur.Core.Text;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class SnippetExpanderTests
{
    private static readonly Snippet[] Snippets =
    {
        new() { Trigger = "my email", Expansion = "ganim@example.com" },
        new() { Trigger = "signature", Expansion = "Best regards,\nGanim" },
    };

    [Theory]
    [InlineData("my email")]
    [InlineData("My Email")]   // case-insensitive
    [InlineData("my email.")]  // trailing punctuation ignored
    [InlineData("  my email ")] // surrounding whitespace ignored
    public void Expand_WholeUtteranceTrigger_ReturnsExpansion(string spoken)
        => Assert.Equal("ganim@example.com", SnippetExpander.Expand(spoken, Snippets));

    [Fact]
    public void Expand_MultilineExpansion_Works()
        => Assert.Equal("Best regards,\nGanim", SnippetExpander.Expand("signature", Snippets));

    [Fact]
    public void Expand_NonMatching_ReturnsOriginal()
        => Assert.Equal("send my email to bob", SnippetExpander.Expand("send my email to bob", Snippets));

    [Fact]
    public void Expand_NoSnippets_ReturnsOriginal()
    {
        Assert.Equal("hello", SnippetExpander.Expand("hello", null));
        Assert.Equal("hello", SnippetExpander.Expand("hello", Array.Empty<Snippet>()));
    }

    [Fact]
    public void Expand_IgnoresBlankTrigger()
    {
        var snippets = new[] { new Snippet { Trigger = "", Expansion = "x" } };
        Assert.Equal("anything", SnippetExpander.Expand("anything", snippets));
    }
}
