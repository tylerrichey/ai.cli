using Ai.Cli;
using Ai.Cli.Generation;
using Ai.Cli.Output;

namespace Ai.Cli.Tests;

public sealed class AiApplicationTests
{
    [Fact]
    public async Task RunAsync_PrintsAndCopiesPowerShellCommand()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("Get-ChildItem", ShellTarget.PowerShell)
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("Get-ChildItem" + Environment.NewLine, stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal("Get-ChildItem", clipboard.LastText);
        Assert.NotNull(service.LastGenerationRequest);
        Assert.Null(service.LastGenerationRequest!.ShellTarget);
        Assert.Equal("list files", service.LastGenerationRequest.Goal);
    }

    [Fact]
    public async Task RunAsync_WrapsBashCommandsBeforePrintingAndCopying()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("ls -la", ShellTarget.Bash)
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--bash", "list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("bash -lc \"ls -la\"" + Environment.NewLine, stdout.ToString());
        Assert.Equal("bash -lc \"ls -la\"", clipboard.LastText);
        Assert.Equal(ShellTarget.Bash, service.LastGenerationRequest!.ShellTarget);
    }

    [Fact]
    public async Task RunAsync_ShellOptionPassesShellTargetToService()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("ls -la", ShellTarget.Zsh)
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--shell", "zsh", "list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("zsh -lc \"ls -la\"" + Environment.NewLine, stdout.ToString());
        Assert.Equal("zsh -lc \"ls -la\"", clipboard.LastText);
        Assert.Equal(ShellTarget.Zsh, service.LastGenerationRequest!.ShellTarget);
    }

    [Fact]
    public async Task RunAsync_ShellOptionAcceptsPowerShell()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("Get-ChildItem", ShellTarget.PowerShell)
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--shell", "powershell", "list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(ShellTarget.PowerShell, service.LastGenerationRequest!.ShellTarget);
    }

    [Fact]
    public async Task RunAsync_BashAndShellTogetherReturnsError()
    {
        var service = new StubAiApplicationService();
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--bash", "--shell", "bash", "list", "files"], CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("--bash", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("--shell", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, service.GenerateCallCount);
    }

    [Fact]
    public async Task RunAsync_InvalidShellValueReturnsError()
    {
        var service = new StubAiApplicationService();
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--shell", "fish", "list", "files"], CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("fish", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, service.GenerateCallCount);
    }

    [Fact]
    public async Task RunAsync_PrintsModelListWithoutUsingClipboard()
    {
        var service = new StubAiApplicationService
        {
            Models = ["alpha/model", "beta/model"]
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--models"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("alpha/model" + Environment.NewLine + "beta/model" + Environment.NewLine, stdout.ToString());
        Assert.Null(clipboard.LastText);
        Assert.Equal(0, service.GenerateCallCount);
        Assert.Equal(1, service.ModelsCallCount);
    }

    [Fact]
    public async Task RunAsync_WarnsWhenClipboardCopyFails()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("Get-ChildItem", ShellTarget.PowerShell)
        };
        var clipboard = new ThrowingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("Get-ChildItem" + Environment.NewLine, stdout.ToString());
        Assert.Contains("clipboard", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WithTiming_PrintsAiClipboardAndTotalDurationsToStandardError()
    {
        var service = new StubAiApplicationService
        {
            GeneratedResult = new GeneratedCommand("Get-ChildItem", ShellTarget.PowerShell)
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--timing", "list", "files"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        var errorOutput = stderr.ToString();
        Assert.Contains("timing.ai_ms=", errorOutput, StringComparison.Ordinal);
        Assert.Contains("timing.clipboard_ms=", errorOutput, StringComparison.Ordinal);
        Assert.Contains("timing.total_ms=", errorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithTimingAndModels_PrintsModelTimingToStandardError()
    {
        var service = new StubAiApplicationService
        {
            Models = ["alpha/model"]
        };
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(["--timing", "--models"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        var errorOutput = stderr.ToString();
        Assert.Contains("timing.models_ms=", errorOutput, StringComparison.Ordinal);
        Assert.Contains("timing.total_ms=", errorOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("timing.clipboard_ms=", errorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PrintsVersionWithoutCallingServices()
    {
        var service = new StubAiApplicationService();
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(
            service,
            clipboard,
            stdout,
            stderr,
            versionProvider: () => "1.0.123.456");

        var exitCode = await application.RunAsync(["--version"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("1.0.123.456" + Environment.NewLine, stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(0, service.GenerateCallCount);
        Assert.Equal(0, service.ModelsCallCount);
        Assert.Null(clipboard.LastText);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorWhenGoalIsMissing()
    {
        var service = new StubAiApplicationService();
        var clipboard = new RecordingClipboardService();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new AiApplication(service, clipboard, stdout, stderr);

        var exitCode = await application.RunAsync(Array.Empty<string>(), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("goal", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    private sealed class StubAiApplicationService : IAiApplicationService
    {
        public GeneratedCommand GeneratedResult { get; set; } = new("", ShellTarget.PowerShell);

        public IReadOnlyList<string> Models { get; set; } = Array.Empty<string>();

        public GenerateUserCommandRequest? LastGenerationRequest { get; private set; }

        public int GenerateCallCount { get; private set; }

        public int ModelsCallCount { get; private set; }

        public Task<GeneratedCommand> GenerateCommandAsync(GenerateUserCommandRequest request, CancellationToken cancellationToken)
        {
            GenerateCallCount++;
            LastGenerationRequest = request;
            return Task.FromResult(GeneratedResult);
        }

        public Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken)
        {
            ModelsCallCount++;
            return Task.FromResult(Models);
        }
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            LastText = text;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Clipboard unavailable.");
        }
    }
}
