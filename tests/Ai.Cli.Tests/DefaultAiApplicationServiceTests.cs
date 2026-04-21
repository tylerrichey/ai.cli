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
                ModelOverride: "override-model",
                IncludedFiles: []),
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
    public async Task GenerateCommandAsync_IncludesCollectedFileContextInPrompt()
    {
        var userProfile = Path.Combine(_rootPath, "profile-files");
        var configDirectory = Path.Combine(userProfile, ".config", "ai");
        var currentDirectory = Path.Combine(_rootPath, "cwd-files");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
            {
              "apiKey": "config-key",
              "defaultModel": "config-model"
            }
            """);
        File.WriteAllText(Path.Combine(currentDirectory, "notes.txt"), "use ripgrep first");

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

        await service.GenerateCommandAsync(
            new GenerateUserCommandRequest(
                Goal: "search notes",
                ShellTarget: ShellTarget.PowerShell,
                ModelOverride: null,
                IncludedFiles: ["notes.txt"]),
            CancellationToken.None);

        Assert.NotNull(client.LastGenerateRequest);
        Assert.Contains("Included file context:", client.LastGenerateRequest!.Prompt, StringComparison.Ordinal);
        Assert.Contains("Path: notes.txt", client.LastGenerateRequest.Prompt, StringComparison.Ordinal);
        Assert.Contains("use ripgrep first", client.LastGenerateRequest.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateCommandAsync_WithPriorMessages_CallsGenerateTextWithMessagesAsync()
    {
        var userProfile = Path.Combine(_rootPath, "profile-resume-cmd");
        var configDirectory = Path.Combine(userProfile, ".config", "ai");
        var currentDirectory = Path.Combine(_rootPath, "cwd-resume-cmd");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
            {
              "apiKey": "config-key",
              "defaultModel": "config-model"
            }
            """);

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

        var priorMessages = new List<ConversationMessage>
        {
            new("user", "first command"),
            new("assistant", "Get-ChildItem")
        };

        var result = await service.GenerateCommandAsync(
            new GenerateUserCommandRequest(
                Goal: "list hidden files",
                ShellTarget: ShellTarget.PowerShell,
                ModelOverride: null,
                IncludedFiles: [],
                PriorMessages: priorMessages),
            CancellationToken.None);

        Assert.Equal("resume answer text", result.RawCommand);
        Assert.NotNull(client.LastTextWithMessagesRequest);
        var (_, _, messages) = client.LastTextWithMessagesRequest!.Value;
        Assert.Equal(3, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("first command", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("Get-ChildItem", messages[1].Content);
        Assert.Equal("user", messages[2].Role);
        Assert.Contains("Goal: list hidden files", messages[2].Content);
    }

    [Fact]
    public async Task AskQuestionAsync_UsesTextGenerationAndIncludesFileContext()
    {
        var userProfile = Path.Combine(_rootPath, "profile-question");
        var configDirectory = Path.Combine(userProfile, ".config", "ai");
        var currentDirectory = Path.Combine(_rootPath, "cwd-question");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), """
            {
              "apiKey": "config-key",
              "defaultModel": "config-model"
            }
            """);
        File.WriteAllText(Path.Combine(currentDirectory, "notes.txt"), "use ripgrep first");

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

        var result = await service.AskQuestionAsync(
            new AskQuestionRequest(
                Question: "What does notes.txt say?",
                ModelOverride: "override-model",
                IncludedFiles: ["notes.txt"]),
            CancellationToken.None);

        Assert.Equal("answer text", result.Answer);
        Assert.NotNull(client.LastTextRequest);
        Assert.Equal("env-key", client.LastTextRequest!.ApiKey);
        Assert.Equal("override-model", client.LastTextRequest.ModelId);
        Assert.Contains("Question: What does notes.txt say?", client.LastTextRequest.Prompt, StringComparison.Ordinal);
        Assert.Contains("Path: notes.txt", client.LastTextRequest.Prompt, StringComparison.Ordinal);
        Assert.Contains("use ripgrep first", client.LastTextRequest.Prompt, StringComparison.Ordinal);
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

        public GenerateCommandRequest? LastTextRequest { get; private set; }

        public string? LastApiKeyForModels { get; private set; }

        public (string ApiKey, string ModelId, IReadOnlyList<ConversationMessage> Messages)? LastTextWithMessagesRequest { get; private set; }

        public Task<string> GenerateCommandAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken)
        {
            LastGenerateRequest = requestModel;
            return Task.FromResult("generated-command");
        }

        public Task<string> GenerateTextAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken)
        {
            LastTextRequest = requestModel;
            return Task.FromResult("answer text");
        }

        public Task<IReadOnlyList<string>> GetModelIdsAsync(string apiKey, CancellationToken cancellationToken)
        {
            LastApiKeyForModels = apiKey;
            return Task.FromResult<IReadOnlyList<string>>(["alpha/model", "beta/model"]);
        }

        public Task<string> GenerateTextWithMessagesAsync(string apiKey, string modelId, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
        {
            LastTextWithMessagesRequest = (apiKey, modelId, messages.ToList());
            return Task.FromResult("resume answer text");
        }
    }
}
