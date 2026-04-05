using System.Diagnostics;

namespace Ai.Cli.Tests;

public sealed class UpdateToolScriptTests
{
    [Fact]
    public void DryRun_PrintsPackAndUpdateCommands()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "update-tool.ps1");

        using var process = StartScript(
            scriptPath,
            repositoryRoot,
            "-DryRun",
            "-Mode",
            "Update",
            "-Configuration",
            "Release");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Contains("dotnet pack", standardOutput, StringComparison.Ordinal);
        Assert.Contains(@"src\Ai.Cli\Ai.Cli.csproj", standardOutput, StringComparison.Ordinal);
        Assert.Contains("dotnet tool update --global --add-source", standardOutput, StringComparison.Ordinal);
        Assert.Contains(@"install-powershell-wrapper.ps1", standardOutput, StringComparison.Ordinal);
        Assert.Contains(@"src\Ai.Cli\bin\Release", standardOutput, StringComparison.Ordinal);
        Assert.Contains("Ai.Cli", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_CanPrintInstallCommandWhenRequested()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "update-tool.ps1");

        using var process = StartScript(
            scriptPath,
            repositoryRoot,
            "-DryRun",
            "-Mode",
            "Install",
            "-Configuration",
            "Release");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Contains("dotnet tool install --global --add-source", standardOutput, StringComparison.Ordinal);
        Assert.Contains("Ai.Cli", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_DefaultModePrintsUpdateCommand()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "update-tool.ps1");

        using var process = StartScript(
            scriptPath,
            repositoryRoot,
            "-DryRun",
            "-Configuration",
            "Release");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Contains("dotnet tool update --global --add-source", standardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool install --global --add-source", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_PrintsExplicitVersionAndToolPathWhenProvided()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "update-tool.ps1");
        var toolPath = Path.Combine(repositoryRoot, ".tmp-tool-path");

        using var process = StartScript(
            scriptPath,
            repositoryRoot,
            "-DryRun",
            "-Configuration",
            "Release",
            "-Version",
            "1.0.123.456",
            "-ToolPath",
            toolPath);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Contains("/p:Version=1.0.123.456", standardOutput, StringComparison.Ordinal);
        Assert.Contains("/p:InformationalVersion=1.0.123.456", standardOutput, StringComparison.Ordinal);
        Assert.Contains("dotnet tool update --tool-path", standardOutput, StringComparison.Ordinal);
        Assert.Contains("--configfile", standardOutput, StringComparison.Ordinal);
        Assert.Contains("--ignore-failed-sources", standardOutput, StringComparison.Ordinal);
        Assert.Contains("NuGet.Config", standardOutput, StringComparison.Ordinal);
        Assert.Contains(toolPath, standardOutput, StringComparison.Ordinal);
    }

    private static Process StartScript(string scriptPath, string workingDirectory, params string[] arguments)
    {
        var argumentList = string.Join(
            " ",
            arguments.Select(QuoteForPowerShell));

        return Process.Start(new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -File {QuoteForPowerShell(scriptPath)} {argumentList}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        }) ?? throw new InvalidOperationException("Failed to start PowerShell.");
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static string QuoteForPowerShell(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
