using Ai.Cli.Generation;

namespace Ai.Cli.OpenRouter;

public interface IOpenRouterClient
{
    Task<IReadOnlyList<string>> GetModelIdsAsync(string apiKey, CancellationToken cancellationToken);

    Task<string> GenerateCommandAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken);

    Task<string> GenerateTextAsync(GenerateCommandRequest requestModel, CancellationToken cancellationToken);

    Task<string> GenerateTextWithMessagesAsync(string apiKey, string modelId, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken);
}
