using Ai.Cli.Generation;

namespace Ai.Cli.Configuration;

public static class ConfigurationResolver
{
    private static readonly Uri DefaultBaseUrl = new("https://openrouter.ai/api/v1/");

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

    public static Uri ResolveBaseUrl(AiConfiguration configuration, string? environmentBaseUrl)
    {
        var rawBaseUrl = string.IsNullOrWhiteSpace(environmentBaseUrl)
            ? configuration.BaseUrl
            : environmentBaseUrl;

        if (string.IsNullOrWhiteSpace(rawBaseUrl))
        {
            return DefaultBaseUrl;
        }

        if (!Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AiConfigurationException(
                $"Invalid baseUrl value '{rawBaseUrl}'. Provide an absolute URL such as 'https://openrouter.ai/api/v1/' or 'http://localhost:1234/v1/'.");
        }

        return uri;
    }

    public static ResolvedGenerationSettings ResolveGenerationSettings(
        AiConfiguration configuration,
        string? environmentApiKey,
        string? modelOverride)
    {
        var apiKey = string.IsNullOrWhiteSpace(environmentApiKey)
            ? configuration.ApiKey
            : environmentApiKey;

        var modelId = modelOverride ?? configuration.DefaultModel;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new AiConfigurationException(
                "No model is configured. Pass --model, set defaultModel in the config file, or use --models to discover one.");
        }

        return new ResolvedGenerationSettings(
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            modelId);
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
