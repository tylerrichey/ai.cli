using Ai.Cli.Configuration;

namespace Ai.Cli.Tests;

public sealed class AiConfigurationLoaderTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"ai-config-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Load_ReturnsEmptyConfigurationWhenFileDoesNotExist()
    {
        Directory.CreateDirectory(_rootPath);

        var configuration = AiConfigurationLoader.Load(Path.Combine(_rootPath, "missing.json"));

        Assert.Null(configuration.ApiKey);
        Assert.Null(configuration.DefaultModel);
        Assert.Null(configuration.DefaultShell);
        Assert.Null(configuration.DefaultMode);
    }

    [Fact]
    public void Load_ReadsApiKeyAndDefaultModelFromJson()
    {
        Directory.CreateDirectory(_rootPath);
        var filePath = Path.Combine(_rootPath, "config.json");
        File.WriteAllText(filePath, """
            {
              "apiKey": "config-key",
              "defaultModel": "openai/test-model"
            }
            """);

        var configuration = AiConfigurationLoader.Load(filePath);

        Assert.Equal("config-key", configuration.ApiKey);
        Assert.Equal("openai/test-model", configuration.DefaultModel);
        Assert.Null(configuration.DefaultShell);
        Assert.Null(configuration.DefaultMode);
    }

    [Fact]
    public void Load_ReadsDefaultShellFromJson()
    {
        Directory.CreateDirectory(_rootPath);
        var filePath = Path.Combine(_rootPath, "config.json");
        File.WriteAllText(filePath, """
            {
              "apiKey": "config-key",
              "defaultModel": "openai/test-model",
              "defaultShell": "zsh"
            }
            """);

        var configuration = AiConfigurationLoader.Load(filePath);

        Assert.Equal("zsh", configuration.DefaultShell);
    }

    [Fact]
    public void Load_ReadsDefaultModeFromJson()
    {
        Directory.CreateDirectory(_rootPath);
        var filePath = Path.Combine(_rootPath, "config.json");
        File.WriteAllText(filePath, """
            {
              "apiKey": "config-key",
              "defaultModel": "openai/test-model",
              "defaultMode": "clipboard"
            }
            """);

        var configuration = AiConfigurationLoader.Load(filePath);

        Assert.Equal("clipboard", configuration.DefaultMode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
