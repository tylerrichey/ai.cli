namespace Ai.Cli.Generation;

public sealed record GenerateUserCommandRequest(string Goal, ShellTarget? ShellTarget, string? ModelOverride);
