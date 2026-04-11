using Ai.Cli.Configuration;

namespace Ai.Cli.Tests;

public sealed class ConfigFileLocatorTests
{
    [Fact]
    public void GetConfigPath_UsesDotConfigUnderUserProfileOnWindows()
    {
        var path = ConfigFileLocator.GetConfigPath(
            operatingSystem: OperatingSystemKind.Windows,
            userProfile: @"C:\Users\Tyler",
            xdgConfigHome: null,
            homeDirectory: null);

        Assert.Equal(@"C:\Users\Tyler\.config\ai\config.json", path);
    }

    [Fact]
    public void GetConfigPath_PrefersXdgConfigHomeOnUnix()
    {
        var path = ConfigFileLocator.GetConfigPath(
            operatingSystem: OperatingSystemKind.Linux,
            userProfile: null,
            xdgConfigHome: "/tmp/xdg",
            homeDirectory: "/home/tyler");

        Assert.Equal("/tmp/xdg/ai/config.json", path);
    }

    [Fact]
    public void GetConfigPath_FallsBackToDotConfigInHomeOnUnix()
    {
        var path = ConfigFileLocator.GetConfigPath(
            operatingSystem: OperatingSystemKind.MacOS,
            userProfile: null,
            xdgConfigHome: null,
            homeDirectory: "/Users/tyler");

        Assert.Equal("/Users/tyler/.config/ai/config.json", path);
    }

    [Fact]
    public void GetHistoryPath_UsesDotConfigUnderUserProfileOnWindows()
    {
        var path = ConfigFileLocator.GetHistoryPath(
            operatingSystem: OperatingSystemKind.Windows,
            userProfile: @"C:\Users\Tyler",
            xdgConfigHome: null,
            homeDirectory: null);

        Assert.Equal(@"C:\Users\Tyler\.config\ai\history.jsonl", path);
    }

    [Fact]
    public void GetHistoryPath_PrefersXdgConfigHomeOnUnix()
    {
        var path = ConfigFileLocator.GetHistoryPath(
            operatingSystem: OperatingSystemKind.Linux,
            userProfile: null,
            xdgConfigHome: "/tmp/xdg",
            homeDirectory: "/home/tyler");

        Assert.Equal("/tmp/xdg/ai/history.jsonl", path);
    }

    [Fact]
    public void GetHistoryPath_FallsBackToDotConfigInHomeOnUnix()
    {
        var path = ConfigFileLocator.GetHistoryPath(
            operatingSystem: OperatingSystemKind.MacOS,
            userProfile: null,
            xdgConfigHome: null,
            homeDirectory: "/Users/tyler");

        Assert.Equal("/Users/tyler/.config/ai/history.jsonl", path);
    }
}
