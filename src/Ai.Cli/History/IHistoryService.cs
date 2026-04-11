namespace Ai.Cli.History;

public interface IHistoryService
{
    Task RecordAsync(HistoryEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoryEntry>> SearchAsync(string? searchTerm, CancellationToken cancellationToken);
}
