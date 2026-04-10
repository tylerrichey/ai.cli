using Ai.Cli.Generation;

namespace Ai.Cli;

public interface IAiApplicationService
{
    Task<GeneratedCommand> GenerateCommandAsync(GenerateUserCommandRequest request, CancellationToken cancellationToken);

    Task<string> AskQuestionAsync(AskQuestionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken);
}
