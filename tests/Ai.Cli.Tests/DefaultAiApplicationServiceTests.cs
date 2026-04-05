using Ai.Cli.Generation;
using Ai.Cli.OpenRouter;

namespace Ai.Cli.Tests;

public sealed class DefaultAiApplicationServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"ai-service-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task GenerateCommandAsync_UsesModelOverrideAndDirectoryContext()
    {
        var userProfile = Path.Combine(_rootPath, "profile");
        var configDirectory = Path.Combine(userProfile, ".config", "ai");
        var currentDirectory = Path.Combine(_rootPath, "cwd");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
            {
              "apiKey": "config-key",
              "defaultModel": "config-model"
            }
            """);
        File.WriteAllText(Path.Combine(currentDirectory, "alpha.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(currentDirectory, "beta"));

        var client = new FakeOpenRouterClient();
        var service = new DefaultAiApplicationService(
            client,
            currentDirectoryProvider: () => currentDirectory,
            environmentVariableReader: name => name switch
            {
                "OPENROUTER_API_KEY" => "env-key",
                "USERPROFILE" => userProfile,
                _ => null
            });

        var result = await service.GenerateCommandAsync(
            new GenerateUserCommandRequest(
                Goal: "list files",
                ShellTarget: ShellTarget.PowerShell,
                ModelOverride: "override-model"),
            CancellationToken.None);

        Assert.Equal("generated-command", result.RawCommand);
        Assert.Equal(ShellTarget.PowerShell, result.ShellTarget);
        Assert.NotNull(client.LastGenerateRequest);
        Assert.Equal("env-key", client.LastGenerateRequest!.ApiKey);
        Assert.Equal("override-model", client.LastGenerateRequest.ModelId);
        Assert.Contains("Goal: list files", client.LastGenerateRequest.Prompt, StringComparison.Ordinal);
        Assert.Contains("- alpha.txt", client.LastGenerateRequest.Prompt, StringComparison.Ordinal);
        Assert.Contains("- beta", client.LastGenerateRequest.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetModelsAsync_UsesEnvironmentApiKeyOverConfig()
    {
        var userProfile = Path.Combine(_rootPath, "profile-models");
        var configDirectory = Path.Combine(userProfile, ".config", "ai");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
            {
              "apiKey": "config-key",
              "defaultModel": "config-model"
            }
            """);

        var client = new FakeOpenRouterClient();
        var service = new DefaultAiApplicationService(
            client,
            currentDirectoryProvider: () => _rootPath,
            environmentVariableReader: name => name switch
            {
                "OPENROUTER_API_KEY" => "env-key",
                "USERPROFILE" => userProfile,
                _ => null
            });

        var models = await service.GetModelsAsync(CancellationToken.None);

        Assert.Equal(["alpha/model", "beta/model"], models);
        Assert.Equal("env-key", client.LastApiKeyForModels);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class FakeOpenRouterClient : IOpenRouterClient
    {
        public GenerateCommandRequest? LastGenerateRequest { get; private set; }

        public string? LastApiKeyForModels { get; private set; }

        public Task<string> GenerateCommandAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken)
        {
            LastGenerateRequest = requestModel;
            return Task.FromResult("generated-command");
        }

        public Task<IReadOnlyList<string>> GetModelIdsAsync(string apiKey, CancellationToken cancellationToken)
        {
            LastApiKeyForModels = apiKey;
            return Task.FromResult<IReadOnlyList<string>>(["alpha/model", "beta/model"]);
        }
    }
}
