namespace Ai.Cli.History;

public sealed record HistoryEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    HistoryEntryKind Kind,
    string Input,
    string Response,
    string? ShellTarget,
    string ModelId,
    string WorkingDirectory,
    IReadOnlyList<string> IncludedFiles,
    bool WasExecuted,
    Guid? ResumedFromId = null);
