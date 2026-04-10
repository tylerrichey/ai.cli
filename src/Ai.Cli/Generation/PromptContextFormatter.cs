using Ai.Cli.Context;

namespace Ai.Cli.Generation;

internal static class PromptContextFormatter
{
    public static void AddFileContext(ICollection<string> builder, IReadOnlyList<FileContext> fileContexts)
    {
        if (fileContexts.Count == 0)
        {
            return;
        }

        builder.Add("Included file context:");
        foreach (var fileContext in fileContexts)
        {
            builder.Add($"Path: {fileContext.DisplayPath}");
            builder.Add("Content:");
            builder.Add(fileContext.Content);

            if (fileContext.WasTruncated)
            {
                builder.Add(
                    $"File content truncated to {fileContext.Content.Length} of {fileContext.OriginalCharacterCount} characters.");
            }
        }
    }
}
