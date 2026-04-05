namespace Ai.Cli.Configuration;

public static class ConfigFileLocator
{
    public static string GetConfigPath(
        OperatingSystemKind operatingSystem,
        string? userProfile,
        string? xdgConfigHome,
        string? homeDirectory)
    {
        return operatingSystem switch
        {
            OperatingSystemKind.Windows => Path.Combine(
                userProfile ?? throw new InvalidOperationException("Windows config lookup requires a user profile path."),
                ".config",
                "ai",
                "config.json"),
            _ => JoinPosix(
                xdgConfigHome ?? JoinPosix(
                    homeDirectory ?? throw new InvalidOperationException("Unix config lookup requires a home directory."),
                    ".config"),
                "ai",
                "config.json")
        };
    }

    private static string JoinPosix(params string[] parts)
    {
        var normalizedParts = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Replace('\\', '/'))
            .ToList();

        if (normalizedParts.Count == 0)
        {
            return string.Empty;
        }

        var prefix = normalizedParts[0].StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;
        normalizedParts[0] = normalizedParts[0].Trim('/');

        return prefix + string.Join("/", normalizedParts.Select(part => part.Trim('/')));
    }
}
