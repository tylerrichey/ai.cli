namespace Ai.Cli.Generation;

public sealed record GenerateCommandRequest(string ApiKey, string ModelId, string Prompt);
