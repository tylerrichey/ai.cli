namespace Ai.Cli;

public interface ICommandExecutor
{
    bool IsInteractive { get; }

    ConsoleKeyInfo ReadKey();

    Task<int> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
