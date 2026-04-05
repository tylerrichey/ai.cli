namespace Ai.Cli.Context;

public static class DirectoryContextCollector
{
    public const int MaxEntryCount = 200;

    public static DirectoryContext Collect(string rootPath)
    {
        var allEntryNames = Directory
            .EnumerateFileSystemEntries(rootPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var entryNames = allEntryNames
            .Take(MaxEntryCount)
            .ToArray();

        return new DirectoryContext(
            RootPath: rootPath,
            EntryNames: entryNames,
            TotalEntries: allEntryNames.Count,
            WasTruncated: allEntryNames.Count > MaxEntryCount);
    }
}
