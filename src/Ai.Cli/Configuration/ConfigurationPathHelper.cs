namespace Ai.Cli.Configuration;

public static class ConfigurationPathHelper
{
    public static string GetConfigPath(
        OperatingSystemKind operatingSystem,
        Func<string, string?>? environmentVariableReader = null)
    {
        var read = environmentVariableReader ?? Environment.GetEnvironmentVariable;

        return ConfigFileLocator.GetConfigPath(
            operatingSystem,
            userProfile: read("USERPROFILE"),
            xdgConfigHome: read("XDG_CONFIG_HOME"),
            homeDirectory: read("HOME"));
    }
}
