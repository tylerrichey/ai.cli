using Ai.Cli.Context;

namespace Ai.Cli.Tests;

public sealed class FileContextCollectorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"ai-file-context-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Collect_ResolvesRelativeAndAbsolutePathsInArgumentOrder()
    {
        Directory.CreateDirectory(_rootPath);
        var relativePath = Path.Combine(_rootPath, "alpha.txt");
        var absolutePath = Path.Combine(_rootPath, "beta.txt");
        File.WriteAllText(relativePath, "alpha");
        File.WriteAllText(absolutePath, "beta");

        var contexts = FileContextCollector.Collect(_rootPath, ["alpha.txt", absolutePath]);

        Assert.Equal(2, contexts.Count);
        Assert.Equal("alpha.txt", contexts[0].DisplayPath);
        Assert.Equal(Path.GetFullPath(relativePath), contexts[0].FullPath);
        Assert.Equal("alpha", contexts[0].Content);
        Assert.Equal(absolutePath, contexts[1].DisplayPath);
        Assert.Equal(Path.GetFullPath(absolutePath), contexts[1].FullPath);
        Assert.Equal("beta", contexts[1].Content);
    }

    [Fact]
    public void Collect_ThrowsWhenMoreThanThreeFilesAreRequested()
    {
        Directory.CreateDirectory(_rootPath);

        var exception = Assert.Throws<ArgumentException>(() =>
            FileContextCollector.Collect(_rootPath, ["a.txt", "b.txt", "c.txt", "d.txt"]));

        Assert.Contains("3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Collect_ThrowsWhenAFileIsMissing()
    {
        Directory.CreateDirectory(_rootPath);

        Assert.Throws<FileNotFoundException>(() => FileContextCollector.Collect(_rootPath, ["missing.txt"]));
    }

    [Fact]
    public void Collect_ThrowsWhenPathIsDirectory()
    {
        var directoryPath = Path.Combine(_rootPath, "nested");
        Directory.CreateDirectory(directoryPath);

        var exception = Assert.Throws<InvalidOperationException>(() => FileContextCollector.Collect(_rootPath, ["nested"]));

        Assert.Contains("directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Collect_ThrowsWhenFileLooksBinary()
    {
        Directory.CreateDirectory(_rootPath);
        File.WriteAllBytes(Path.Combine(_rootPath, "image.bin"), [0x00, 0x01, 0x02, 0x03]);

        var exception = Assert.Throws<InvalidOperationException>(() => FileContextCollector.Collect(_rootPath, ["image.bin"]));

        Assert.Contains("binary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Collect_ReallocatesUnusedBudgetToLaterFiles()
    {
        Directory.CreateDirectory(_rootPath);
        File.WriteAllText(Path.Combine(_rootPath, "short.txt"), new string('a', 1000));
        File.WriteAllText(Path.Combine(_rootPath, "long.txt"), new string('b', 13000));

        var contexts = FileContextCollector.Collect(_rootPath, ["short.txt", "long.txt"]);

        Assert.Equal(1000, contexts[0].Content.Length);
        Assert.False(contexts[0].WasTruncated);
        Assert.Equal(11000, contexts[1].Content.Length);
        Assert.True(contexts[1].WasTruncated);
        Assert.Equal(13000, contexts[1].OriginalCharacterCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
