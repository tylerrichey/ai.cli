using Ai.Cli.Generation;

namespace Ai.Cli.Output;

public static class ShellCommandFormatter
{
    public static string FormatForOutput(string commandBody, ShellTarget shellTarget)
    {
        return shellTarget switch
        {
            ShellTarget.PowerShell => commandBody.Trim(),
            ShellTarget.Bash => WrapForUnixShell(commandBody, "bash"),
            ShellTarget.Zsh => WrapForUnixShell(commandBody, "zsh"),
            _ => throw new ArgumentOutOfRangeException(nameof(shellTarget), shellTarget, null)
        };
    }

    private static string WrapForUnixShell(string commandBody, string shellName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandBody);

        var trimmedCommand = commandBody.Trim();
        var prefix = $"{shellName} -lc ";
        if (trimmedCommand.StartsWith(prefix, StringComparison.Ordinal))
        {
            return trimmedCommand;
        }

        return $"{shellName} -lc \"{trimmedCommand.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
