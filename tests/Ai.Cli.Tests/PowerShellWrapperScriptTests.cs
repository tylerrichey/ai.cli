using System.Diagnostics;
using System.Text;

namespace Ai.Cli.Tests;

public sealed class PowerShellWrapperScriptTests
{
    [Fact]
    public void InstallScript_WritesWrapperAndManagedProfileBlock()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "install-powershell-wrapper.ps1");
        var tempDirectory = CreateTemporaryDirectory(repositoryRoot);
        var profilePath = Path.Combine(tempDirectory, "Microsoft.PowerShell_profile.ps1");
        var wrapperPath = Path.Combine(tempDirectory, "ai-wrapper.ps1");
        var fakeGeneratorPath = CreateFakeGenerator(tempDirectory, "Write-Output 'install test'");

        using var process = StartPowerShellFile(
            repositoryRoot,
            scriptPath,
            "-ProfilePath",
            profilePath,
            "-WrapperPath",
            wrapperPath,
            "-GeneratorPath",
            fakeGeneratorPath);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(File.Exists(scriptPath), $"Expected script at '{scriptPath}'.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardOutput);
        Assert.Equal(string.Empty, standardError);
        Assert.True(File.Exists(wrapperPath), $"Expected wrapper at '{wrapperPath}'.");
        Assert.True(File.Exists(profilePath), $"Expected profile at '{profilePath}'.");

        var profileContents = File.ReadAllText(profilePath);
        Assert.Contains("# >>> ai-pwsh wrapper >>>", profileContents, StringComparison.Ordinal);
        Assert.Contains("# <<< ai-pwsh wrapper <<<", profileContents, StringComparison.Ordinal);
        Assert.Contains($". '{wrapperPath}'", profileContents, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallScript_DoesNotDuplicateManagedProfileBlock()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "install-powershell-wrapper.ps1");
        var tempDirectory = CreateTemporaryDirectory(repositoryRoot);
        var profilePath = Path.Combine(tempDirectory, "Microsoft.PowerShell_profile.ps1");
        var wrapperPath = Path.Combine(tempDirectory, "ai-wrapper.ps1");
        var fakeGeneratorPath = CreateFakeGenerator(tempDirectory, "Write-Output 'install test'");

        using (var firstProcess = StartPowerShellFile(
                   repositoryRoot,
                   scriptPath,
                   "-ProfilePath",
                   profilePath,
                   "-WrapperPath",
                   wrapperPath,
                   "-GeneratorPath",
                   fakeGeneratorPath))
        {
            firstProcess.WaitForExit();
            Assert.Equal(0, firstProcess.ExitCode);
        }

        using (var secondProcess = StartPowerShellFile(
                   repositoryRoot,
                   scriptPath,
                   "-ProfilePath",
                   profilePath,
                   "-WrapperPath",
                   wrapperPath,
                   "-GeneratorPath",
                   fakeGeneratorPath))
        {
            secondProcess.WaitForExit();
            Assert.Equal(0, secondProcess.ExitCode);
        }

        var profileContents = File.ReadAllText(profilePath);
        Assert.Equal(1, CountOccurrences(profileContents, "# >>> ai-pwsh wrapper >>>"));
        Assert.Equal(1, CountOccurrences(profileContents, "# <<< ai-pwsh wrapper <<<"));
        Assert.Equal(1, CountOccurrences(profileContents, $". '{wrapperPath}'"));
    }

    [Fact]
    public void WrapperFunction_ExecutesGeneratedCommandWhenDecisionIsEnter()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "install-powershell-wrapper.ps1");
        var tempDirectory = CreateTemporaryDirectory(repositoryRoot);
        var profilePath = Path.Combine(tempDirectory, "Microsoft.PowerShell_profile.ps1");
        var wrapperPath = Path.Combine(tempDirectory, "ai-wrapper.ps1");
        var resultPath = Path.Combine(tempDirectory, "executed.txt");
        var fakeGeneratorPath = Path.Combine(tempDirectory, "fake-ai.ps1");

        File.WriteAllText(
            fakeGeneratorPath,
            """
            param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CommandArgs)
            "Set-Content -Path '__RESULT_PATH__' -Value 'executed'"
            """.Replace("__RESULT_PATH__", EscapeForSingleQuotedPowerShell(resultPath), StringComparison.Ordinal));

        using (var installProcess = StartPowerShellFile(
                   repositoryRoot,
                   scriptPath,
                   "-ProfilePath",
                   profilePath,
                   "-WrapperPath",
                   wrapperPath,
                   "-GeneratorPath",
                   fakeGeneratorPath))
        {
            installProcess.WaitForExit();
            Assert.Equal(0, installProcess.ExitCode);
        }

        var command = $". '{EscapeForSingleQuotedPowerShell(wrapperPath)}'; ai create file";
        using var process = StartPowerShellCommand(
            repositoryRoot,
            command,
            new Dictionary<string, string?>
            {
                ["AI_WRAPPER_DECISION"] = "enter"
            });

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.True(File.Exists(resultPath), "Expected generated command to execute.");
        Assert.Equal("executed" + Environment.NewLine, File.ReadAllText(resultPath));
        Assert.Contains("Set-Content -Path", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void WrapperFunction_CancelsGeneratedCommandWhenDecisionIsNotEnter()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "install-powershell-wrapper.ps1");
        var tempDirectory = CreateTemporaryDirectory(repositoryRoot);
        var profilePath = Path.Combine(tempDirectory, "Microsoft.PowerShell_profile.ps1");
        var wrapperPath = Path.Combine(tempDirectory, "ai-wrapper.ps1");
        var resultPath = Path.Combine(tempDirectory, "cancelled.txt");
        var fakeGeneratorPath = Path.Combine(tempDirectory, "fake-ai.ps1");

        File.WriteAllText(
            fakeGeneratorPath,
            """
            param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CommandArgs)
            "Set-Content -Path '__RESULT_PATH__' -Value 'executed'"
            """.Replace("__RESULT_PATH__", EscapeForSingleQuotedPowerShell(resultPath), StringComparison.Ordinal));

        using (var installProcess = StartPowerShellFile(
                   repositoryRoot,
                   scriptPath,
                   "-ProfilePath",
                   profilePath,
                   "-WrapperPath",
                   wrapperPath,
                   "-GeneratorPath",
                   fakeGeneratorPath))
        {
            installProcess.WaitForExit();
            Assert.Equal(0, installProcess.ExitCode);
        }

        var command = $". '{EscapeForSingleQuotedPowerShell(wrapperPath)}'; ai create file";
        using var process = StartPowerShellCommand(
            repositoryRoot,
            command,
            new Dictionary<string, string?>
            {
                ["AI_WRAPPER_DECISION"] = "cancel"
            });

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.False(File.Exists(resultPath), "Expected generated command to be skipped.");
        Assert.Contains("Set-Content -Path", standardOutput, StringComparison.Ordinal);
        Assert.Contains("Cancelled.", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void WrapperFunction_PassesThroughModelsWithoutPrompt()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "install-powershell-wrapper.ps1");
        var tempDirectory = CreateTemporaryDirectory(repositoryRoot);
        var profilePath = Path.Combine(tempDirectory, "Microsoft.PowerShell_profile.ps1");
        var wrapperPath = Path.Combine(tempDirectory, "ai-wrapper.ps1");
        var fakeGeneratorPath = Path.Combine(tempDirectory, "fake-ai.ps1");

        File.WriteAllText(
            fakeGeneratorPath,
            """
            param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CommandArgs)

            if ($CommandArgs -contains '--models') {
                'alpha/model'
                'beta/model'
                exit 0
            }

            'Write-Output "unexpected"'
            """);

        using (var installProcess = StartPowerShellFile(
                   repositoryRoot,
                   scriptPath,
                   "-ProfilePath",
                   profilePath,
                   "-WrapperPath",
                   wrapperPath,
                   "-GeneratorPath",
                   fakeGeneratorPath))
        {
            installProcess.WaitForExit();
            Assert.Equal(0, installProcess.ExitCode);
        }

        var command = $". '{EscapeForSingleQuotedPowerShell(wrapperPath)}'; ai --models";
        using var process = StartPowerShellCommand(
            repositoryRoot,
            command,
            new Dictionary<string, string?>
            {
                ["AI_WRAPPER_DECISION"] = "cancel"
            });

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Contains("alpha/model", standardOutput, StringComparison.Ordinal);
        Assert.Contains("beta/model", standardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelled.", standardOutput, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static string CreateTemporaryDirectory(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, ".tmp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFakeGenerator(string directoryPath, string outputExpression)
    {
        var fakeGeneratorPath = Path.Combine(directoryPath, "fake-ai.ps1");
        File.WriteAllText(
            fakeGeneratorPath,
            $$"""
            param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CommandArgs)
            {{outputExpression}}
            """);
        return fakeGeneratorPath;
    }

    private static string EscapeForSingleQuotedPowerShell(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static Process StartPowerShellCommand(
        string workingDirectory,
        string command,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        return StartPowerShellProcess(
            workingDirectory,
            $"-NoProfile -Command {QuoteForPowerShell(command)}",
            environmentVariables);
    }

    private static Process StartPowerShellFile(
        string workingDirectory,
        string scriptPath,
        params string[] arguments)
    {
        var renderedArguments = string.Join(" ", arguments.Select(QuoteForPowerShell));
        return StartPowerShellProcess(
            workingDirectory,
            $"-NoProfile -File {QuoteForPowerShell(scriptPath)} {renderedArguments}");
    }

    private static Process StartPowerShellProcess(
        string workingDirectory,
        string arguments,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
    }

    private static string QuoteForPowerShell(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                _ => character
            });
        }

        builder.Append('"');
        return builder.ToString();
    }
}
