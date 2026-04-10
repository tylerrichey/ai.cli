using Ai.Cli.Context;
using Ai.Cli.Generation;

namespace Ai.Cli.Tests;

public sealed class QuestionPromptBuilderTests
{
    [Fact]
    public void Build_IncludesQuestionDirectoryAndFileContextWithoutShellInstructions()
    {
        var directoryContext = new DirectoryContext(
            RootPath: @"C:\work",
            EntryNames: ["alpha.txt"],
            TotalEntries: 1,
            WasTruncated: false);
        FileContext[] fileContexts =
        [
            new FileContext(
                DisplayPath: "alpha.txt",
                FullPath: @"C:\work\alpha.txt",
                Content: "first line",
                OriginalCharacterCount: 10,
                WasTruncated: false)
        ];

        var prompt = QuestionPromptBuilder.Build(
            question: "What does this file do?",
            operatingSystemDescription: "Windows",
            directoryContext: directoryContext,
            fileContexts: fileContexts);

        Assert.Contains("Answer the user's question directly in plain text.", prompt, StringComparison.Ordinal);
        Assert.Contains("Question: What does this file do?", prompt, StringComparison.Ordinal);
        Assert.Contains(@"Current directory: C:\work", prompt, StringComparison.Ordinal);
        Assert.Contains("Included file context:", prompt, StringComparison.Ordinal);
        Assert.Contains("Path: alpha.txt", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Target shell:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Return only one runnable command line", prompt, StringComparison.Ordinal);
    }
}
