using System.Diagnostics;

namespace Ai.Cli.Tests;

public sealed class ReleaseTagScriptTests
{
    [Fact]
    public void DryRun_BumpsLatestPatchTag()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "release-tag.ps1");
        var tempRepository = CreateTemporaryGitRepository(repositoryRoot);

        try
        {
            RunProcess("git", tempRepository, "tag", "v1.0.1");

            using var process = StartScript(
                scriptPath,
                repositoryRoot,
                "-RepositoryRoot",
                tempRepository,
                "-DryRun");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(string.Empty, standardError);
            Assert.Contains("git", standardOutput, StringComparison.Ordinal);
            Assert.Contains("tag v1.0.2", standardOutput, StringComparison.Ordinal);
            Assert.Contains("push origin v1.0.2", standardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(tempRepository);
        }
    }

    [Fact]
    public void DryRun_UsesExplicitTagWhenProvided()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "release-tag.ps1");
        var tempRepository = CreateTemporaryGitRepository(repositoryRoot);

        try
        {
            using var process = StartScript(
                scriptPath,
                repositoryRoot,
                "-RepositoryRoot",
                tempRepository,
                "-Tag",
                "v2.0.0",
                "-DryRun");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(string.Empty, standardError);
            Assert.Contains("tag v2.0.0", standardOutput, StringComparison.Ordinal);
            Assert.Contains("push origin v2.0.0", standardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("v1.0.1", standardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(tempRepository);
        }
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

    private static string CreateTemporaryGitRepository(string repositoryRoot)
    {
        var tempDirectory = Path.Combine(repositoryRoot, ".tmp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        RunProcess("git", tempDirectory, "init");
        RunProcess("git", tempDirectory, "config", "user.email", "tests@example.invalid");
        RunProcess("git", tempDirectory, "config", "user.name", "Release Tag Tests");

        File.WriteAllText(Path.Combine(tempDirectory, "README.md"), "test repo" + Environment.NewLine);
        RunProcess("git", tempDirectory, "add", "README.md");
        RunProcess("git", tempDirectory, "commit", "-m", "Initial commit");

        return tempDirectory;
    }

    private static void RunProcess(string fileName, string workingDirectory, params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        }.ApplyArguments(arguments)) ?? throw new InvalidOperationException($"Failed to start {fileName}.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"{fileName} {string.Join(" ", arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}{standardError}");
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static string QuoteForPowerShell(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo ApplyArguments(this ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
