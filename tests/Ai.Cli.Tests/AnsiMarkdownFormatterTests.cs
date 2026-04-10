using Ai.Cli.Output;

namespace Ai.Cli.Tests;

public sealed class AnsiMarkdownFormatterTests
{
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Italic = "\x1b[3m";
    private const string Underline = "\x1b[4m";
    private const string Cyan = "\x1b[36m";
    private const string Yellow = "\x1b[33m";
    private const string Green = "\x1b[32m";
    private const string Magenta = "\x1b[35m";
    private const string BrightBlack = "\x1b[90m";

    private readonly AnsiMarkdownFormatter _formatter = new();

    [Theory]
    [InlineData("# Heading 1", Bold + Magenta + "# ")]
    [InlineData("## Heading 2", Bold + Cyan + "## ")]
    [InlineData("### Heading 3", Bold + Yellow + "### ")]
    public void Format_Headings_AppliesColorAndBold(string input, string expectedPrefix)
    {
        var result = _formatter.Format(input);

        Assert.StartsWith(expectedPrefix, result, StringComparison.Ordinal);
        Assert.EndsWith(Reset, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_BoldText_WrapsBold()
    {
        var result = _formatter.Format("This is **bold** text.");

        Assert.Contains(Bold + "bold" + Reset, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ItalicText_WrapsItalic()
    {
        var result = _formatter.Format("This is *italic* text.");

        Assert.Contains(Italic + "italic" + Reset, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_InlineCode_WrapsCyan()
    {
        var result = _formatter.Format("Use `dotnet build` to compile.");

        Assert.Contains(Cyan + "dotnet build" + Reset, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_FencedCodeBlock_RendersWithGreenAndIndent()
    {
        var input = """
            ```csharp
            Console.WriteLine("Hello");
            ```
            """;

        var result = _formatter.Format(input);

        Assert.Contains(BrightBlack + "csharp" + Reset, result, StringComparison.Ordinal);
        Assert.Contains(Green, result, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(\"Hello\")", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UnorderedList_RendersBullets()
    {
        var input = """
            - First item
            - Second item
            - Third item
            """;

        var result = _formatter.Format(input);

        Assert.Contains("\u2022 ", result, StringComparison.Ordinal);
        Assert.Contains("First item", result, StringComparison.Ordinal);
        Assert.Contains("Second item", result, StringComparison.Ordinal);
        Assert.Contains("Third item", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OrderedList_RendersNumbers()
    {
        var input = """
            1. First
            2. Second
            3. Third
            """;

        var result = _formatter.Format(input);

        Assert.Contains("1. ", result, StringComparison.Ordinal);
        Assert.Contains("2. ", result, StringComparison.Ordinal);
        Assert.Contains("3. ", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Link_ShowsUrlInParentheses()
    {
        var result = _formatter.Format("See [docs](https://example.com) for more.");

        Assert.Contains(Underline + "docs" + Reset, result, StringComparison.Ordinal);
        Assert.Contains("(https://example.com)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ThematicBreak_RendersHorizontalRule()
    {
        var input = """
            Above

            ---

            Below
            """;

        var result = _formatter.Format(input);

        Assert.Contains("\u2500", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_BlockQuote_RendersWithVerticalBar()
    {
        var result = _formatter.Format("> Quoted text");

        Assert.Contains("\u2502 ", result, StringComparison.Ordinal);
        Assert.Contains("Quoted text", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_PlainText_PassesThroughWithoutExtraAnsi()
    {
        var result = _formatter.Format("Just plain text.");

        Assert.Equal("Just plain text.", result);
    }

    [Fact]
    public void Format_MultipleBlocks_SeparatedByBlankLines()
    {
        var input = """
            # Title

            Some paragraph.
            """;

        var result = _formatter.Format(input);

        Assert.Contains("Title", result, StringComparison.Ordinal);
        Assert.Contains("Some paragraph.", result, StringComparison.Ordinal);
    }
}
