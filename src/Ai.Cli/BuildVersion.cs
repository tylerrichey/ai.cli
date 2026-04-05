using System.Reflection;

namespace Ai.Cli;

public static class BuildVersion
{
    public static string GetDisplayVersion()
    {
        var assembly = typeof(BuildVersion).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
