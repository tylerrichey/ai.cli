using Ai.Cli.Output;

namespace Ai.Cli.Tests;

public sealed class PlainMarkdownFormatterTests
{
    [Fact]
    public void Format_ReturnsInputUnchanged()
    {
        var formatter = new PlainMarkdownFormatter();
        const string markdown = "# Hello\n\nSome **bold** text.";

        var result = formatter.Format(markdown);

        Assert.Equal(markdown, result);
    }
}
