using System.Diagnostics;

namespace Ai.Cli;

public sealed class ProcessCommandExecutor : ICommandExecutor
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    public async Task<int> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
