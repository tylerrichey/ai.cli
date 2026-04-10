# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`ai` is a .NET global tool that turns natural-language goals into shell commands or answers workspace questions using OpenRouter. It supports PowerShell, bash, and zsh targets for command generation. The default shell is platform-specific (Windows→PowerShell, Linux→bash, macOS→zsh) and can be overridden via `defaultShell` in config or `--shell`/`--bash` on the CLI. In PowerShell it prompts the user to execute generated commands via a managed wrapper function, while question mode passes straight through.

## Build and test commands

```bash
# Restore + test
dotnet restore tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj
dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore

# Run a single test by fully-qualified name
dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~ClassName.MethodName"

# Pack the global tool
dotnet pack src/Ai.Cli/Ai.Cli.csproj -c Release

# Build + install/update (PowerShell)
./scripts/update-tool.ps1            # update existing install
./scripts/update-tool.ps1 -Mode Install  # first-time install
```

## Architecture

The project targets **net10.0** with nullable enabled. It uses `System.CommandLine` for CLI parsing and `TextCopy` for clipboard access. Tests use xunit.

### Request flow

`Program.cs` wires up the object graph and calls `AiApplication.RunAsync(args)`.

1. **AiApplication** (`AiApplication.cs`) — owns CLI parsing via `System.CommandLine`. Handles `--version` as a fast path before parsing. Routes to model listing, question answering (`-q`), or command generation. With `--execute`/`-x`, displays the generated command on stderr, prompts for Enter-to-confirm, and runs it via `ICommandExecutor`; otherwise generated commands are copied to the clipboard. Question answers print to stdout only.
2. **DefaultAiApplicationService** (`IAiApplicationService` / `DefaultAiApplicationService.cs`) — orchestrates command or question requests: resolves config, collects directory/file context, builds the prompt, calls OpenRouter.
3. **Configuration layer** (`Configuration/`) — `AiConfigurationLoader` reads JSON from a platform-specific path resolved by `ConfigFileLocator`. `ConfigurationResolver` merges file config, env vars (`OPENROUTER_API_KEY`), and CLI overrides into `ResolvedGenerationSettings`, and resolves the shell target via `ResolveShellTarget` (CLI flag > config `defaultShell` > OS default).
4. **Context** (`Context/`) — `DirectoryContextCollector` lists up to 200 entries in the current directory to include in the prompt. `FileContextCollector` resolves up to 3 `-f` paths, rejects missing/directories/binary-looking files, and includes up to 12,000 characters of file content across them.
5. **Generation** (`Generation/`) — `GenerationPromptBuilder` assembles command prompts from goal, shell target, OS, directory context, and optional file context. `QuestionPromptBuilder` assembles plain-text question prompts. `ShellTarget` enum has PowerShell, Bash, and Zsh. `GenerateCommandAsync` returns a `GeneratedCommand` record carrying both the raw command and the resolved shell target.
6. **OpenRouter** (`OpenRouter/`) — `OpenRouterClient` calls the OpenRouter chat completions API and model listing endpoint. Command responses are collapsed to a single runnable line; question responses preserve internal newlines.
7. **Output** (`Output/`) — `ShellCommandFormatter.FormatForOutput` handles all shell targets: PowerShell commands are trimmed, bash/zsh commands are wrapped in `<shell> -lc "..."`. `GetExecutionCommand` returns the shell binary and arguments needed to run a command directly (used by `--execute`). `SystemClipboardService` wraps TextCopy. Question answers bypass clipboard handling.
8. **Execution** (`ICommandExecutor` / `ProcessCommandExecutor`) — runs a generated command as a child process. `ProcessCommandExecutor` checks `Console.IsInputRedirected` for interactivity, reads a confirmation key press, and delegates to `Process.Start`. Injected into `AiApplication` for testability.

### PowerShell wrapper

`scripts/install-powershell-wrapper.ps1` generates a wrapper script at `~/.config/ai/powershell-wrapper.ps1` that defines a `global:ai` function. This function calls `ai.exe`, displays generated commands, and prompts with Enter-to-execute or any-key-to-cancel. Pass-through invocations such as `--models`, `--version`, and `-q` bypass the prompt. The wrapper is dot-sourced from the user's PowerShell profile via managed marker comments (`# >>> ai-pwsh wrapper >>>`).

### Testing patterns

Tests reference the main project directly. Dependencies are injected via constructor parameters — `DefaultAiApplicationService` accepts optional `Func<string>` and `Func<string, string?>` delegates for current directory and env var access. `AiApplication` accepts `IAiApplicationService`, `IClipboardService`, `TextWriter` for stdout/stderr, and an optional version provider. No DI container is used.

### CI / Release

`.github/workflows/release.yml` triggers on `v*` tags. It packs the tool with the tag as the version and pushes the NuGet package to nuget.org using the `NUGET_API_KEY` secret.

### NuGet configuration

`Directory.Build.props` forces all projects to use the repo-local `NuGet.Config` and restore packages into `.nuget/`. This keeps the build self-contained.
