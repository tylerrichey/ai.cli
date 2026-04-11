using Ai.Cli.Configuration;
using Ai.Cli.Context;
using Ai.Cli.Generation;
using Ai.Cli.OpenRouter;

namespace Ai.Cli;

public sealed class DefaultAiApplicationService(
    IOpenRouterClient openRouterClient,
    Func<string>? currentDirectoryProvider = null,
    Func<string, string?>? environmentVariableReader = null)
    : IAiApplicationService
{
    private readonly IOpenRouterClient _openRouterClient = openRouterClient;
    private readonly Func<string> _currentDirectoryProvider = currentDirectoryProvider ?? Directory.GetCurrentDirectory;
    private readonly Func<string, string?> _environmentVariableReader = environmentVariableReader ?? Environment.GetEnvironmentVariable;

    public async Task<GeneratedCommand> GenerateCommandAsync(GenerateUserCommandRequest request, CancellationToken cancellationToken)
    {
        var operatingSystem = GetOperatingSystemKind();
        var configuration = AiConfigurationLoader.Load(GetConfigPath(operatingSystem));
        var settings = ConfigurationResolver.ResolveGenerationSettings(
            configuration,
            _environmentVariableReader("OPENROUTER_API_KEY"),
            request.ModelOverride);
        var shellTarget = ConfigurationResolver.ResolveShellTarget(
            configuration,
            request.ShellTarget,
            operatingSystem);
        var currentDirectory = _currentDirectoryProvider();
        var directoryContext = DirectoryContextCollector.Collect(currentDirectory);
        var fileContexts = FileContextCollector.Collect(currentDirectory, request.IncludedFiles);
        var prompt = GenerationPromptBuilder.Build(
            request.Goal,
            shellTarget,
            operatingSystem.ToString(),
            directoryContext,
            fileContexts);

        var rawCommand = await _openRouterClient.GenerateCommandAsync(
            new GenerateCommandRequest(settings.ApiKey, settings.ModelId, prompt),
            cancellationToken);

        return new GeneratedCommand(rawCommand, shellTarget, settings.ModelId);
    }

    public async Task<GeneratedAnswer> AskQuestionAsync(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        var operatingSystem = GetOperatingSystemKind();
        var configuration = AiConfigurationLoader.Load(GetConfigPath(operatingSystem));
        var settings = ConfigurationResolver.ResolveGenerationSettings(
            configuration,
            _environmentVariableReader("OPENROUTER_API_KEY"),
            request.ModelOverride);
        var currentDirectory = _currentDirectoryProvider();
        var fileContexts = FileContextCollector.Collect(currentDirectory, request.IncludedFiles);

        if (request.PriorMessages is { Count: > 0 })
        {
            var allMessages = new List<ConversationMessage>(request.PriorMessages);

            var lines = new List<string> { request.Question };
            PromptContextFormatter.AddFileContext(lines, fileContexts);
            allMessages.Add(new ConversationMessage("user", string.Join(Environment.NewLine, lines)));

            var resumeAnswer = await _openRouterClient.GenerateTextWithMessagesAsync(
                settings.ApiKey, settings.ModelId, allMessages, cancellationToken);

            return new GeneratedAnswer(resumeAnswer, settings.ModelId);
        }

        var directoryContext = DirectoryContextCollector.Collect(currentDirectory);
        var prompt = QuestionPromptBuilder.Build(
            request.Question,
            operatingSystem.ToString(),
            directoryContext,
            fileContexts);

        var answer = await _openRouterClient.GenerateTextAsync(
            new GenerateCommandRequest(settings.ApiKey, settings.ModelId, prompt),
            cancellationToken);

        return new GeneratedAnswer(answer, settings.ModelId);
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken)
    {
        var operatingSystem = GetOperatingSystemKind();
        var configuration = AiConfigurationLoader.Load(GetConfigPath(operatingSystem));
        var apiKey = _environmentVariableReader("OPENROUTER_API_KEY") ?? configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationException(
                "No OpenRouter API key is configured. Set OPENROUTER_API_KEY or add apiKey to the config file.");
        }

        return await _openRouterClient.GetModelIdsAsync(apiKey, cancellationToken);
    }

    private string GetConfigPath(OperatingSystemKind operatingSystem)
    {
        return ConfigFileLocator.GetConfigPath(
            operatingSystem,
            userProfile: _environmentVariableReader("USERPROFILE"),
            xdgConfigHome: _environmentVariableReader("XDG_CONFIG_HOME"),
            homeDirectory: _environmentVariableReader("HOME"));
    }

    private static OperatingSystemKind GetOperatingSystemKind()
    {
        if (OperatingSystem.IsWindows())
        {
            return OperatingSystemKind.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return OperatingSystemKind.MacOS;
        }

        return OperatingSystemKind.Linux;
    }
}
