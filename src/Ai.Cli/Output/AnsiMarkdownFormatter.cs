using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Ai.Cli.Output;

public sealed class AnsiMarkdownFormatter : IMarkdownFormatter
{
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim = "\x1b[2m";
    private const string Italic = "\x1b[3m";
    private const string Underline = "\x1b[4m";
    private const string Cyan = "\x1b[36m";
    private const string Yellow = "\x1b[33m";
    private const string Green = "\x1b[32m";
    private const string Magenta = "\x1b[35m";
    private const string BrightBlack = "\x1b[90m";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    public string Format(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var sb = new StringBuilder();
        RenderBlocks(sb, document);

        // Trim trailing blank lines added by block spacing
        while (sb.Length >= Environment.NewLine.Length &&
               sb.ToString(sb.Length - Environment.NewLine.Length, Environment.NewLine.Length) == Environment.NewLine)
        {
            sb.Length -= Environment.NewLine.Length;
        }

        return sb.ToString();
    }

    private static void RenderBlocks(StringBuilder sb, ContainerBlock container)
    {
        var isFirst = true;
        foreach (var block in container)
        {
            if (!isFirst)
            {
                sb.AppendLine();
            }

            isFirst = false;
            RenderBlock(sb, block, indent: "");
        }
    }

    private static void RenderBlock(StringBuilder sb, Block block, string indent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(sb, heading, indent);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(sb, paragraph, indent);
                break;
            case FencedCodeBlock fencedCode:
                RenderFencedCodeBlock(sb, fencedCode, indent);
                break;
            case CodeBlock code:
                RenderCodeBlock(sb, code, indent);
                break;
            case ListBlock list:
                RenderList(sb, list, indent);
                break;
            case ListItemBlock listItem:
                RenderListItem(sb, listItem, indent);
                break;
            case ThematicBreakBlock:
                sb.Append(indent);
                sb.Append(Dim);
                sb.Append(new string('\u2500', 40));
                sb.Append(Reset);
                sb.AppendLine();
                break;
            case QuoteBlock quote:
                RenderQuoteBlock(sb, quote, indent);
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    RenderBlock(sb, child, indent);
                }

                break;
            default:
                // Fallback: render any leaf block as plain text
                if (block is LeafBlock leaf)
                {
                    RenderLeafPlain(sb, leaf, indent);
                }

                break;
        }
    }

    private static void RenderHeading(StringBuilder sb, HeadingBlock heading, string indent)
    {
        sb.Append(indent);
        var (prefix, color) = heading.Level switch
        {
            1 => ("# ", Bold + Magenta),
            2 => ("## ", Bold + Cyan),
            3 => ("### ", Bold + Yellow),
            _ => (new string('#', heading.Level) + " ", Bold)
        };
        sb.Append(color);
        sb.Append(prefix);
        if (heading.Inline is not null)
        {
            RenderInlines(sb, heading.Inline, color);
        }

        sb.Append(Reset);
        sb.AppendLine();
    }

    private static void RenderParagraph(StringBuilder sb, ParagraphBlock paragraph, string indent)
    {
        sb.Append(indent);
        if (paragraph.Inline is not null)
        {
            RenderInlines(sb, paragraph.Inline, "");
        }

        sb.AppendLine();
    }

    private static void RenderFencedCodeBlock(StringBuilder sb, FencedCodeBlock fencedCode, string indent)
    {
        var lang = fencedCode.Info;
        if (!string.IsNullOrEmpty(lang))
        {
            sb.Append(indent);
            sb.Append(BrightBlack);
            sb.Append(lang);
            sb.Append(Reset);
            sb.AppendLine();
        }

        RenderCodeBlock(sb, fencedCode, indent);
    }

    private static void RenderCodeBlock(StringBuilder sb, CodeBlock code, string indent)
    {
        var codeIndent = indent + "  ";
        foreach (var line in code.Lines)
        {
            var text = line.ToString();
            sb.Append(codeIndent);
            sb.Append(Green);
            sb.Append(text);
            sb.Append(Reset);
            sb.AppendLine();
        }
    }

    private static void RenderList(StringBuilder sb, ListBlock list, string indent)
    {
        var index = list.IsOrdered ? (list.OrderedStart is not null && int.TryParse(list.OrderedStart, out var start) ? start : 1) : 0;
        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
            {
                continue;
            }

            var bullet = list.IsOrdered ? $"{index}. " : "\u2022 ";
            if (list.IsOrdered)
            {
                index++;
            }

            RenderListItem(sb, listItem, indent, bullet);
        }
    }

    private static void RenderListItem(StringBuilder sb, ListItemBlock listItem, string indent, string bullet = "\u2022 ")
    {
        var isFirst = true;
        foreach (var child in listItem)
        {
            if (isFirst)
            {
                sb.Append(indent);
                sb.Append(Yellow);
                sb.Append(bullet);
                sb.Append(Reset);

                if (child is ParagraphBlock paragraph && paragraph.Inline is not null)
                {
                    RenderInlines(sb, paragraph.Inline, "");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine();
                    RenderBlock(sb, child, indent + "  ");
                }

                isFirst = false;
            }
            else
            {
                RenderBlock(sb, child, indent + "  ");
            }
        }
    }

    private static void RenderQuoteBlock(StringBuilder sb, QuoteBlock quote, string indent)
    {
        var quoteIndent = indent + BrightBlack + "\u2502 " + Reset;
        foreach (var child in quote)
        {
            RenderBlock(sb, child, quoteIndent);
        }
    }

    private static void RenderInlines(StringBuilder sb, ContainerInline container, string activeStyle)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content);
                    break;
                case EmphasisInline emphasis:
                    var emphasisCode = emphasis.DelimiterCount >= 2 ? Bold : Italic;
                    sb.Append(emphasisCode);
                    RenderInlines(sb, emphasis, emphasisCode);
                    sb.Append(Reset);
                    if (!string.IsNullOrEmpty(activeStyle))
                    {
                        sb.Append(activeStyle);
                    }

                    break;
                case CodeInline code:
                    sb.Append(Cyan);
                    sb.Append(code.Content);
                    sb.Append(Reset);
                    if (!string.IsNullOrEmpty(activeStyle))
                    {
                        sb.Append(activeStyle);
                    }

                    break;
                case LinkInline link:
                    if (link.IsImage)
                    {
                        sb.Append(BrightBlack);
                        sb.Append("[image: ");
                        RenderInlines(sb, link, BrightBlack);
                        sb.Append(']');
                        sb.Append(Reset);
                    }
                    else
                    {
                        sb.Append(Underline);
                        RenderInlines(sb, link, Underline);
                        sb.Append(Reset);
                        if (!string.IsNullOrEmpty(link.Url))
                        {
                            sb.Append(BrightBlack);
                            sb.Append(" (");
                            sb.Append(link.Url);
                            sb.Append(')');
                            sb.Append(Reset);
                        }
                    }

                    if (!string.IsNullOrEmpty(activeStyle))
                    {
                        sb.Append(activeStyle);
                    }

                    break;
                case LineBreakInline lineBreak:
                    sb.AppendLine(lineBreak.IsHard ? "" : "");
                    break;
                case HtmlInline html:
                    sb.Append(html.Tag);
                    break;
                default:
                    // Fallback for unknown inline types
                    if (inline is ContainerInline nestedContainer)
                    {
                        RenderInlines(sb, nestedContainer, activeStyle);
                    }

                    break;
            }
        }
    }

    private static void RenderLeafPlain(StringBuilder sb, LeafBlock leaf, string indent)
    {
        sb.Append(indent);
        if (leaf.Inline is not null)
        {
            RenderInlines(sb, leaf.Inline, "");
        }
        else
        {
            foreach (var line in leaf.Lines)
            {
                sb.Append(line.ToString());
            }
        }

        sb.AppendLine();
    }
}
