using Ai.Cli.Context;

namespace Ai.Cli.Tests;

public sealed class DirectoryContextCollectorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"ai-cli-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Collect_SortsNamesAlphabetically()
    {
        Directory.CreateDirectory(_rootPath);
        File.WriteAllText(Path.Combine(_rootPath, "zeta.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(_rootPath, "alpha"));
        File.WriteAllText(Path.Combine(_rootPath, "middle.txt"), string.Empty);

        var context = DirectoryContextCollector.Collect(_rootPath);

        Assert.Equal(["alpha", "middle.txt", "zeta.txt"], context.EntryNames);
        Assert.False(context.WasTruncated);
    }

    [Fact]
    public void Collect_CapsEntriesAtTwoHundredAndFlagsTruncation()
    {
        Directory.CreateDirectory(_rootPath);
        for (var index = 0; index < 205; index++)
        {
            File.WriteAllText(Path.Combine(_rootPath, $"file-{index:D3}.txt"), string.Empty);
        }

        var context = DirectoryContextCollector.Collect(_rootPath);

        Assert.Equal(200, context.EntryNames.Count);
        Assert.True(context.WasTruncated);
        Assert.Equal("file-000.txt", context.EntryNames[0]);
        Assert.Equal("file-199.txt", context.EntryNames[^1]);
        Assert.Equal(205, context.TotalEntries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
