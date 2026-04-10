using Ai.Cli.Context;

namespace Ai.Cli.Generation;

public static class QuestionPromptBuilder
{
    public static string Build(
        string question,
        string operatingSystemDescription,
        DirectoryContext directoryContext,
        IReadOnlyList<FileContext> fileContexts)
    {
        var builder = new List<string>
        {
            "You are answering a question for a human operator.",
            "Answer the user's question directly in plain text.",
            "Operating system: " + operatingSystemDescription,
            "Current directory: " + directoryContext.RootPath,
            "Question: " + question,
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
