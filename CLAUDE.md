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

1. **AiApplication** (`AiApplication.cs`) — owns CLI parsing via `System.CommandLine`. Handles `--version` as a fast path before parsing. Routes to history search (`-hs`/`--history`), model listing, resume (`-r`/`--resume`), question answering (`-q`), or command generation. Loads config (override via optional `Func<AiConfiguration>` for tests) to merge **`defaultMode`** with explicit `-q`/`-x`: valid values are `question`, `execute`, and `clipboard` (case-insensitive). If `defaultMode` is omitted or the config file is missing, the implicit default is **`execute`**. Explicit `-q` or `-x` always wins over config. **`--clipboard-only`** forces the generate-to-stdout + clipboard path for that invocation so a configured execute default does not run the internal execute path (the PowerShell wrapper passes this flag). With the merged execute path, displays the generated command on stderr, prompts for Enter-to-confirm, and runs it via `ICommandExecutor`; with the merged clipboard path, generated commands are copied to the clipboard. Question answers and resume continuations that stay in question mode print to stdout only. Resume (`-r`) may combine with merged execute or `-x`: that path generates a shell command using prior history as conversation context (including when the prior chain was a question) and uses the same execute prompt as plain generation; explicit `-r` with `-q` remains invalid. Records each successful invocation to `IHistoryService` unless `--no-history`/`-nh` is set.
2. **DefaultAiApplicationService** (`IAiApplicationService` / `DefaultAiApplicationService.cs`) — orchestrates command or question requests: resolves config, collects directory/file context, builds the prompt, calls OpenRouter. When `AskQuestionRequest.PriorMessages` is set, skips `QuestionPromptBuilder` and calls `GenerateTextWithMessagesAsync` with the full conversation array instead.
3. **Configuration layer** (`Configuration/`) — `AiConfigurationLoader` reads JSON from a platform-specific path resolved by `ConfigFileLocator` / `ConfigurationPathHelper`. `ConfigurationResolver` merges file config, env vars (`OPENROUTER_API_KEY`), and CLI overrides into `ResolvedGenerationSettings`, resolves the shell target via `ResolveShellTarget` (CLI flag > config `defaultShell` > OS default), and resolves `defaultMode` via `ResolveDefaultInvocationMode` for implicit question vs execute vs clipboard behavior (see `AiApplication`).
4. **Context** (`Context/`) — `DirectoryContextCollector` lists up to 200 entries in the current directory to include in the prompt. `FileContextCollector` resolves up to 3 `-f` paths, rejects missing/directories/binary-looking files, and includes up to 12,000 characters of file content across them.
5. **Generation** (`Generation/`) — `GenerationPromptBuilder` assembles command prompts from goal, shell target, OS, directory context, and optional file context. `QuestionPromptBuilder` assembles plain-text question prompts. `ConversationMessage` (role + content) represents a single turn in a multi-turn exchange. `ShellTarget` enum has PowerShell, Bash, and Zsh. `GenerateCommandAsync` returns a `GeneratedCommand` record (raw command, shell target, model ID). `AskQuestionAsync` returns a `GeneratedAnswer` record (answer text, model ID).
6. **OpenRouter** (`OpenRouter/`) — `OpenRouterClient` calls the OpenRouter chat completions API and model listing endpoint. `GenerateCommandAsync` and `GenerateTextAsync` wrap a single prompt into a one-message array. `GenerateTextWithMessagesAsync` accepts a full `IReadOnlyList<ConversationMessage>` for multi-turn conversations. Command responses are collapsed to a single runnable line; text responses preserve internal newlines.
7. **Output** (`Output/`) — `ShellCommandFormatter.FormatForOutput` handles all shell targets: PowerShell commands are trimmed, bash/zsh commands are wrapped in `<shell> -lc "..."`. `GetExecutionCommand` returns the shell binary and arguments needed to run a command directly (used by `--execute`). `SystemClipboardService` wraps TextCopy. Question answers bypass clipboard handling.
8. **Execution** (`ICommandExecutor` / `ProcessCommandExecutor`) — runs a generated command as a child process. `ProcessCommandExecutor` checks `Console.IsInputRedirected` for interactivity, reads a confirmation key press, and delegates to `Process.Start`. Injected into `AiApplication` for testability.
9. **History** (`History/`) — `IHistoryService` with `RecordAsync` and `SearchAsync`. `JsonlHistoryService` appends one JSON entry per line to `history.jsonl` (same directory as config, resolved by `ConfigFileLocator.GetHistoryPath`). Each entry captures timestamp, kind (command/question/resume), input, response, shell target, model ID, working directory, included files, `wasExecuted`, and `resumedFromId` (nullable Guid linking to the prior entry in a conversation chain). Search is case-insensitive substring match over input and response fields; results are returned newest-first. Old entries without `resumedFromId` deserialize cleanly with null.

### PowerShell wrapper

`scripts/install-powershell-wrapper.ps1` generates a wrapper script at `~/.config/ai/powershell-wrapper.ps1` that defines a `global:ai` function. This function calls `ai.exe` with **`--clipboard-only`** so stdout still receives the formatted command even when `defaultMode` is `execute`, then displays the command and prompts with Enter-to-execute or any-key-to-cancel. Pass-through invocations such as `--models`, `--version`, `-q`, and `-r` bypass the prompt. The wrapper is dot-sourced from the user's PowerShell profile via managed marker comments (`# >>> ai-pwsh wrapper >>>`).

### Testing patterns

Tests reference the main project directly. Dependencies are injected via constructor parameters — `DefaultAiApplicationService` accepts optional `Func<string>` and `Func<string, string?>` delegates for current directory and env var access. `AiApplication` accepts `IAiApplicationService`, `IClipboardService`, `TextWriter` for stdout/stderr, optional `ICommandExecutor`, `versionProvider`, `IMarkdownFormatter`, `IHistoryService`, and optional **`configurationProvider`** (`Func<AiConfiguration>`) so tests do not depend on a real config file. No DI container is used.

### CI / Release

`.github/workflows/release.yml` triggers on `v*` tags. It packs the tool with the tag as the version and pushes the NuGet package to nuget.org using the `NUGET_API_KEY` secret.

### NuGet configuration

`Directory.Build.props` forces all projects to use the repo-local `NuGet.Config` and restore packages into `.nuget/`. This keeps the build self-contained.
