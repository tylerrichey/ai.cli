using Ai.Cli.History;

namespace Ai.Cli.Tests;

public sealed class JsonlHistoryServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"ai-history-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string HistoryPath => Path.Combine(_rootPath, "history.jsonl");

    private static HistoryEntry MakeEntry(
        string input = "list files",
        string response = "Get-ChildItem",
        HistoryEntryKind kind = HistoryEntryKind.Command,
        string? shellTarget = "powershell",
        string modelId = "test-model",
        bool wasExecuted = false,
        DateTimeOffset? timestamp = null) =>
        new(
            Id: Guid.NewGuid(),
            Timestamp: timestamp ?? DateTimeOffset.UtcNow,
            Kind: kind,
            Input: input,
            Response: response,
            ShellTarget: shellTarget,
            ModelId: modelId,
            WorkingDirectory: "/tmp",
            IncludedFiles: [],
            WasExecuted: wasExecuted);

    [Fact]
    public async Task RecordAsync_AppendsEntryToFile()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        var entry = MakeEntry();
        await service.RecordAsync(entry, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(HistoryPath);
        Assert.Single(lines, l => !string.IsNullOrWhiteSpace(l));
    }

    [Fact]
    public async Task RecordAsync_AppendsMultipleEntries()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        await service.RecordAsync(MakeEntry(input: "first"), CancellationToken.None);
        await service.RecordAsync(MakeEntry(input: "second"), CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(HistoryPath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task RecordAsync_CreatesDirectoryIfMissing()
    {
        var deepPath = Path.Combine(_rootPath, "nested", "dir", "history.jsonl");
        var service = new JsonlHistoryService(deepPath);

        await service.RecordAsync(MakeEntry(), CancellationToken.None);

        Assert.True(File.Exists(deepPath));
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyListWhenFileDoesNotExist()
    {
        var service = new JsonlHistoryService(HistoryPath);

        var results = await service.SearchAsync(null, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEntriesInReverseChronologicalOrder()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        var older = MakeEntry(input: "first", timestamp: DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = MakeEntry(input: "second", timestamp: DateTimeOffset.UtcNow);
        await service.RecordAsync(older, CancellationToken.None);
        await service.RecordAsync(newer, CancellationToken.None);

        var results = await service.SearchAsync(null, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("second", results[0].Input);
        Assert.Equal("first", results[1].Input);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySearchTermInInput()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        await service.RecordAsync(MakeEntry(input: "list files"), CancellationToken.None);
        await service.RecordAsync(MakeEntry(input: "create directory"), CancellationToken.None);

        var results = await service.SearchAsync("list", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("list files", results[0].Input);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySearchTermInResponse()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        await service.RecordAsync(MakeEntry(response: "Get-ChildItem"), CancellationToken.None);
        await service.RecordAsync(MakeEntry(response: "mkdir foo"), CancellationToken.None);

        var results = await service.SearchAsync("mkdir", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("mkdir foo", results[0].Response);
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        await service.RecordAsync(MakeEntry(input: "List Files"), CancellationToken.None);

        var results = await service.SearchAsync("list files", CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_SkipsCorruptLines()
    {
        Directory.CreateDirectory(_rootPath);
        await File.WriteAllTextAsync(HistoryPath, "not valid json\n");
        var service = new JsonlHistoryService(HistoryPath);
        await service.RecordAsync(MakeEntry(input: "valid entry"), CancellationToken.None);

        var results = await service.SearchAsync(null, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("valid entry", results[0].Input);
    }

    [Fact]
    public async Task SearchAsync_SkipsBlankLines()
    {
        Directory.CreateDirectory(_rootPath);
        var service = new JsonlHistoryService(HistoryPath);
        await service.RecordAsync(MakeEntry(input: "the entry"), CancellationToken.None);
        await File.AppendAllTextAsync(HistoryPath, "\n\n   \n");

        var results = await service.SearchAsync(null, CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_RoundTripsAllFields()
    {
        var service = new JsonlHistoryService(HistoryPath);
        Directory.CreateDirectory(_rootPath);

        var id = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero);
        var entry = new HistoryEntry(
            Id: id,
            Timestamp: timestamp,
            Kind: HistoryEntryKind.Question,
            Input: "what is dotnet",
            Response: "A runtime and framework...",
            ShellTarget: null,
            ModelId: "openai/gpt-4",
            WorkingDirectory: "/home/tyler",
            IncludedFiles: ["file1.cs", "file2.cs"],
            WasExecuted: false);

        await service.RecordAsync(entry, CancellationToken.None);
        var results = await service.SearchAsync(null, CancellationToken.None);

        Assert.Single(results);
        var r = results[0];
        Assert.Equal(id, r.Id);
        Assert.Equal(timestamp, r.Timestamp);
        Assert.Equal(HistoryEntryKind.Question, r.Kind);
        Assert.Equal("what is dotnet", r.Input);
        Assert.Equal("A runtime and framework...", r.Response);
        Assert.Null(r.ShellTarget);
        Assert.Equal("openai/gpt-4", r.ModelId);
        Assert.Equal("/home/tyler", r.WorkingDirectory);
        Assert.Equal(["file1.cs", "file2.cs"], r.IncludedFiles);
        Assert.False(r.WasExecuted);
    }
}
