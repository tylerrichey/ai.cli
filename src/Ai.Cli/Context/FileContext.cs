namespace Ai.Cli.Context;

public sealed record FileContext(
    string DisplayPath,
    string FullPath,
    string Content,
    int OriginalCharacterCount,
    bool WasTruncated);
