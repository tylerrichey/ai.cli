namespace Ai.Cli.Generation;

public sealed record GeneratedCommand(string RawCommand, ShellTarget ShellTarget, string ModelId);
