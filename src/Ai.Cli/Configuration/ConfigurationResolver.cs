using Ai.Cli.Generation;

namespace Ai.Cli.Configuration;

public static class ConfigurationResolver
{
    public static ShellTarget ResolveShellTarget(
        AiConfiguration configuration,
        ShellTarget? cliOverride,
        OperatingSystemKind operatingSystem)
    {
        if (cliOverride is not null)
        {
            return cliOverride.Value;
        }

        if (!string.IsNullOrWhiteSpace(configuration.DefaultShell))
        {
            if (Enum.TryParse<ShellTarget>(configuration.DefaultShell, ignoreCase: true, out var configuredTarget))
            {
                return configuredTarget;
            }

            throw new AiConfigurationException(
                $"Invalid defaultShell value '{configuration.DefaultShell}'. Valid options: powershell, bash, zsh.");
        }

        return operatingSystem switch
        {
            OperatingSystemKind.Windows => ShellTarget.PowerShell,
            OperatingSystemKind.MacOS => ShellTarget.Zsh,
            OperatingSystemKind.Linux => ShellTarget.Bash,
            _ => ShellTarget.PowerShell
        };
    }

    public static ResolvedGenerationSettings ResolveGenerationSettings(
        AiConfiguration configuration,
        string? environmentApiKey,
        string? modelOverride)
    {
        var apiKey = environmentApiKey ?? configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationException(
                "No OpenRouter API key is configured. Set OPENROUTER_API_KEY or add apiKey to the config file.");
        }

        var modelId = modelOverride ?? configuration.DefaultModel;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new AiConfigurationException(
                "No model is configured. Pass --model, set defaultModel in the config file, or use --models to discover one.");
        }

        return new ResolvedGenerationSettings(apiKey, modelId);
    }

    public static DefaultInvocationMode ResolveDefaultInvocationMode(AiConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.DefaultMode))
        {
            return DefaultInvocationMode.Execute;
        }

        if (string.Equals(configuration.DefaultMode, "question", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultInvocationMode.Question;
        }

        if (string.Equals(configuration.DefaultMode, "execute", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultInvocationMode.Execute;
        }

        if (string.Equals(configuration.DefaultMode, "clipboard", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultInvocationMode.Clipboard;
        }

        throw new AiConfigurationException(
            $"Invalid defaultMode value '{configuration.DefaultMode}'. Valid options: question, execute, clipboard.");
    }
}
