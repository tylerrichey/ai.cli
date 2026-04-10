using Ai.Cli.Context;
using Ai.Cli.Generation;

namespace Ai.Cli.Tests;

public sealed class GenerationPromptBuilderTests
{
    [Fact]
    public void Build_IncludesGoalShellAndDirectoryEntriesForPowerShell()
    {
        var directoryContext = new DirectoryContext(
            RootPath: @"C:\work",
            EntryNames: ["alpha.txt", "beta"],
            TotalEntries: 2,
            WasTruncated: false);

        var prompt = GenerationPromptBuilder.Build(
            goal: "list all files and group them",
            shellTarget: ShellTarget.PowerShell,
            operatingSystemDescription: "Windows",
            directoryContext: directoryContext,
            fileContexts: []);

        Assert.Contains("Goal: list all files and group them", prompt, StringComparison.Ordinal);
        Assert.Contains("Target shell: PowerShell", prompt, StringComparison.Ordinal);
        Assert.Contains(@"Current directory: C:\work", prompt, StringComparison.Ordinal);
        Assert.Contains("- alpha.txt", prompt, StringComparison.Ordinal);
        Assert.Contains("- beta", prompt, StringComparison.Ordinal);
        Assert.Contains("Return only one runnable command line", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AddsTruncationNoticeWhenDirectoryEntriesAreCapped()
    {
        var directoryContext = new DirectoryContext(
            RootPath: "/tmp/work",
            EntryNames: ["a", "b"],
            TotalEntries: 205,
            WasTruncated: true);

        var prompt = GenerationPromptBuilder.Build(
            goal: "find duplicates",
            shellTarget: ShellTarget.Bash,
            operatingSystemDescription: "Linux",
            directoryContext: directoryContext,
            fileContexts: []);

        Assert.Contains("Only the first 200 entries are included here out of 205 total entries.", prompt, StringComparison.Ordinal);
        Assert.Contains("Return only the bash command body", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludesZshInstructionsForZshTarget()
    {
        var directoryContext = new DirectoryContext(
            RootPath: "/home/user/work",
            EntryNames: ["file.txt"],
            TotalEntries: 1,
            WasTruncated: false);

        var prompt = GenerationPromptBuilder.Build(
            goal: "list files",
            shellTarget: ShellTarget.Zsh,
            operatingSystemDescription: "MacOS",
            directoryContext: directoryContext,
            fileContexts: []);

        Assert.Contains("Target shell: Zsh", prompt, StringComparison.Ordinal);
        Assert.Contains("Return only the zsh command body", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludesFileContextSections()
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
                OriginalCharacterCount: 20,
                WasTruncated: true)
        ];

        var prompt = GenerationPromptBuilder.Build(
            goal: "summarize the file",
            shellTarget: ShellTarget.PowerShell,
            operatingSystemDescription: "Windows",
            directoryContext: directoryContext,
            fileContexts: fileContexts);

        Assert.Contains("Included file context:", prompt, StringComparison.Ordinal);
        Assert.Contains("Path: alpha.txt", prompt, StringComparison.Ordinal);
        Assert.Contains("first line", prompt, StringComparison.Ordinal);
        Assert.Contains("File content truncated to 10 of 20 characters.", prompt, StringComparison.Ordinal);
    }
}
