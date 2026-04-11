using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai.Cli.History;

public sealed class JsonlHistoryService(string historyFilePath) : IHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task RecordAsync(HistoryEntry entry, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(historyFilePath)!);

        var line = JsonSerializer.Serialize(entry, JsonOptions);

        await using var stream = new FileStream(
            historyFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryEntry>> SearchAsync(string? searchTerm, CancellationToken cancellationToken)
    {
        if (!File.Exists(historyFilePath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(historyFilePath, cancellationToken);
        var results = new List<HistoryEntry>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            HistoryEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions);
            }
            catch
            {
                continue;
            }

            if (entry is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(searchTerm) &&
                !entry.Input.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !entry.Response.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(entry);
        }

        results.Reverse();
        return results;
    }
}
