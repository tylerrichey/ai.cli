using Ai.Cli.Generation;
using Ai.Cli.Output;

namespace Ai.Cli.Tests;

public sealed class ShellCommandFormatterTests
{
    [Fact]
    public void FormatForOutput_Bash_UsesBashDashLcForPlainCommands()
    {
        var result = ShellCommandFormatter.FormatForOutput("printf 'hello'", ShellTarget.Bash);

        Assert.Equal("bash -lc \"printf 'hello'\"", result);
    }

    [Fact]
    public void FormatForOutput_Bash_EscapesEmbeddedDoubleQuotes()
    {
        var result = ShellCommandFormatter.FormatForOutput("printf \"hello\"", ShellTarget.Bash);

        Assert.Equal("bash -lc \"printf \\\"hello\\\"\"", result);
    }

    [Fact]
    public void FormatForOutput_Bash_DoesNotDoubleWrapExistingBashLauncher()
    {
        var result = ShellCommandFormatter.FormatForOutput("bash -lc \"ls -la\"", ShellTarget.Bash);

        Assert.Equal("bash -lc \"ls -la\"", result);
    }

    [Fact]
    public void FormatForOutput_Zsh_UsesZshDashLcForPlainCommands()
    {
        var result = ShellCommandFormatter.FormatForOutput("ls -la", ShellTarget.Zsh);

        Assert.Equal("zsh -lc \"ls -la\"", result);
    }

    [Fact]
    public void FormatForOutput_Zsh_EscapesEmbeddedDoubleQuotes()
    {
        var result = ShellCommandFormatter.FormatForOutput("printf \"hello\"", ShellTarget.Zsh);

        Assert.Equal("zsh -lc \"printf \\\"hello\\\"\"", result);
    }

    [Fact]
    public void FormatForOutput_Zsh_DoesNotDoubleWrapExistingZshLauncher()
    {
        var result = ShellCommandFormatter.FormatForOutput("zsh -lc \"ls -la\"", ShellTarget.Zsh);

        Assert.Equal("zsh -lc \"ls -la\"", result);
    }

    [Fact]
    public void FormatForOutput_PowerShell_ReturnsTrimmedCommand()
    {
        var result = ShellCommandFormatter.FormatForOutput("  Get-ChildItem  ", ShellTarget.PowerShell);

        Assert.Equal("Get-ChildItem", result);
    }
}
