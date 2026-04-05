namespace Ai.Cli.Context;

public sealed record DirectoryContext(
    string RootPath,
    IReadOnlyList<string> EntryNames,
    int TotalEntries,
    bool WasTruncated);
