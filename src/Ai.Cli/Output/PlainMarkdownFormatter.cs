namespace Ai.Cli.Output;

public sealed class PlainMarkdownFormatter : IMarkdownFormatter
{
    public string Format(string markdown) => markdown;
}
