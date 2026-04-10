using Ai.Cli.Context;

namespace Ai.Cli.Generation;

public static class GenerationPromptBuilder
{
    public static string Build(
        string goal,
        ShellTarget shellTarget,
        string operatingSystemDescription,
        DirectoryContext directoryContext,
        IReadOnlyList<FileContext> fileContexts)
    {
        var instructions = shellTarget switch
        {
            ShellTarget.PowerShell => "Return only one runnable command line in native PowerShell syntax.",
            ShellTarget.Bash => "Return only the bash command body with no leading `bash` wrapper.",
            ShellTarget.Zsh => "Return only the zsh command body with no leading `zsh` wrapper.",
            _ => throw new ArgumentOutOfRangeException(nameof(shellTarget), shellTarget, null)
        };

        var builder = new List<string>
        {
            "You are generating a shell command for a human operator.",
            instructions,
            "Do not include prose, markdown, code fences, or explanations.",
            $"Operating system: {operatingSystemDescription}",
            $"Target shell: {shellTarget}",
            $"Current directory: {directoryContext.RootPath}",
            $"Goal: {goal}",
            "Immediate entries in the current directory:"
        };

        builder.AddRange(directoryContext.EntryNames.Select(name => $"- {name}"));

        if (directoryContext.WasTruncated)
        {
            builder.Add($"Only the first 200 entries are included here out of {directoryContext.TotalEntries} total entries.");
        }

        PromptContextFormatter.AddFileContext(builder, fileContexts);

        return string.Join(Environment.NewLine, builder);
    }
}
