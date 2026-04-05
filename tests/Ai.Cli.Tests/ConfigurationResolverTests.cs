using Ai.Cli.Configuration;
using Ai.Cli.Generation;

namespace Ai.Cli.Tests;

public sealed class ConfigurationResolverTests
{
    [Fact]
    public void ResolveGenerationSettings_PrefersEnvironmentApiKeyOverConfig()
    {
        var config = new AiConfiguration("config-key", "config-model", null);

        var result = ConfigurationResolver.ResolveGenerationSettings(
            config,
            environmentApiKey: "env-key",
            modelOverride: null);

        Assert.Equal("env-key", result.ApiKey);
        Assert.Equal("config-model", result.ModelId);
    }

    [Fact]
    public void ResolveGenerationSettings_PrefersModelOverrideOverConfig()
    {
        var config = new AiConfiguration("config-key", "config-model", null);

        var result = ConfigurationResolver.ResolveGenerationSettings(
            config,
            environmentApiKey: null,
            modelOverride: "override-model");

        Assert.Equal("config-key", result.ApiKey);
        Assert.Equal("override-model", result.ModelId);
    }

    [Fact]
    public void ResolveGenerationSettings_ThrowsWhenModelIsMissing()
    {
        var config = new AiConfiguration("config-key", null, null);

        var exception = Assert.Throws<AiConfigurationException>(() => ConfigurationResolver.ResolveGenerationSettings(
            config,
            environmentApiKey: null,
            modelOverride: null));

        Assert.Contains("defaultModel", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveShellTarget_CliOverrideWinsOverConfigAndOsDefault()
    {
        var config = new AiConfiguration(null, null, "bash");

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: ShellTarget.Zsh,
            operatingSystem: OperatingSystemKind.Windows);

        Assert.Equal(ShellTarget.Zsh, result);
    }

    [Fact]
    public void ResolveShellTarget_ConfigWinsOverOsDefault()
    {
        var config = new AiConfiguration(null, null, "bash");

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.Windows);

        Assert.Equal(ShellTarget.Bash, result);
    }

    [Fact]
    public void ResolveShellTarget_ConfigIsCaseInsensitive()
    {
        var config = new AiConfiguration(null, null, "ZSH");

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.Windows);

        Assert.Equal(ShellTarget.Zsh, result);
    }

    [Fact]
    public void ResolveShellTarget_DefaultsToPoweShellOnWindows()
    {
        var config = new AiConfiguration(null, null, null);

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.Windows);

        Assert.Equal(ShellTarget.PowerShell, result);
    }

    [Fact]
    public void ResolveShellTarget_DefaultsToBashOnLinux()
    {
        var config = new AiConfiguration(null, null, null);

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.Linux);

        Assert.Equal(ShellTarget.Bash, result);
    }

    [Fact]
    public void ResolveShellTarget_DefaultsToZshOnMacOS()
    {
        var config = new AiConfiguration(null, null, null);

        var result = ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.MacOS);

        Assert.Equal(ShellTarget.Zsh, result);
    }

    [Fact]
    public void ResolveShellTarget_ThrowsOnInvalidConfigValue()
    {
        var config = new AiConfiguration(null, null, "fish");

        var exception = Assert.Throws<AiConfigurationException>(() => ConfigurationResolver.ResolveShellTarget(
            config,
            cliOverride: null,
            operatingSystem: OperatingSystemKind.Linux));

        Assert.Contains("fish", exception.Message, StringComparison.Ordinal);
    }
}
