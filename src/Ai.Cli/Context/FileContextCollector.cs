using System.Text;

namespace Ai.Cli.Context;

public static class FileContextCollector
{
    public const int MaxFileCount = 3;
    public const int MaxTotalCharacterCount = 12000;

    public static IReadOnlyList<FileContext> Collect(string rootPath, IReadOnlyList<string> requestedPaths)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(requestedPaths);

        if (requestedPaths.Count == 0)
        {
            return [];
        }

        if (requestedPaths.Count > MaxFileCount)
        {
            throw new ArgumentException($"A maximum of {MaxFileCount} files can be included with -f.", nameof(requestedPaths));
        }

        var contexts = new List<FileContext>(requestedPaths.Count);
        var remainingBudget = MaxTotalCharacterCount;

        for (var index = 0; index < requestedPaths.Count; index++)
        {
            var displayPath = requestedPaths[index];
            var fullPath = ResolveFullPath(rootPath, displayPath);

            if (Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Included path '{displayPath}' is a directory, not a file.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Included file '{displayPath}' was not found.", fullPath);
            }

            var bytes = File.ReadAllBytes(fullPath);
            if (LooksBinary(bytes))
            {
                throw new InvalidOperationException($"Included file '{displayPath}' appears to be binary and cannot be added as text context.");
            }

            var content = Encoding.UTF8.GetString(bytes);
            var remainingFiles = requestedPaths.Count - index;
            var characterBudget = remainingFiles == 0 ? remainingBudget : remainingBudget / remainingFiles;
            var excerptLength = Math.Min(content.Length, characterBudget);
            var excerpt = content[..excerptLength];

            contexts.Add(new FileContext(
                DisplayPath: displayPath,
                FullPath: fullPath,
                Content: excerpt,
                OriginalCharacterCount: content.Length,
                WasTruncated: excerptLength < content.Length));

            remainingBudget -= excerptLength;
        }

        return contexts;
    }

    private static string ResolveFullPath(string rootPath, string requestedPath)
    {
        if (Path.IsPathRooted(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        return Path.GetFullPath(Path.Combine(rootPath, requestedPath));
    }

    private static bool LooksBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return true;
        }

        var sampleLength = Math.Min(bytes.Length, 8000);
        var controlCharacterCount = 0;
        for (var index = 0; index < sampleLength; index++)
        {
            var currentByte = bytes[index];
            if (currentByte < 0x09 || (currentByte > 0x0D && currentByte < 0x20))
            {
                controlCharacterCount++;
            }
        }

        return controlCharacterCount > sampleLength / 10;
    }
}
