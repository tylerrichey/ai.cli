namespace Ai.Cli.Generation;

public sealed record AskQuestionRequest(
    string Question,
    string? ModelOverride,
    IReadOnlyList<string> IncludedFiles);
