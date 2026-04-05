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
            directoryContext: directoryContext);

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
            directoryContext: directoryContext);

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
            directoryContext: directoryContext);

        Assert.Contains("Target shell: Zsh", prompt, StringComparison.Ordinal);
        Assert.Contains("Return only the zsh command body", prompt, StringComparison.Ordinal);
    }
}
