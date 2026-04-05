# `ai`

`ai` is a .NET global tool that uses OpenRouter to turn natural-language goals into shell commands.

## Features

- `ai <goal...>` generates a PowerShell command, prints it, copies it to the clipboard, and in PowerShell prompts for Enter-to-execute in the current session.
- `ai --bash <goal...>` generates a bash command body and prints it as `bash -lc "<command>"`.
- `ai --shell <target> <goal...>` generates a command for the specified shell (`powershell`, `bash`, `zsh`).
- `ai --models` lists available OpenRouter model IDs alphabetically.
- `ai --model <model-id> <goal...>` overrides the configured default model.
- `ai --version` prints the built tool version.
- `ai --timing <goal...>` prints timing information to stderr, including the AI call duration.
- `ai.exe <goal...>` remains available as the raw generator without the PowerShell execution prompt.

## Configuration

The tool reads JSON config from:

- Windows: `%USERPROFILE%\.config\ai\config.json`
- Unix: `$XDG_CONFIG_HOME/ai/config.json` or `~/.config/ai/config.json`

Supported keys:

```json
{
  "apiKey": "your-openrouter-api-key",
  "defaultModel": "openai/gpt-5-mini",
  "defaultShell": "bash"
}
```

The `defaultShell` key accepts `powershell`, `bash`, or `zsh`. When omitted, the default is platform-specific: PowerShell on Windows, bash on Linux, zsh on macOS.

Environment overrides:

- `OPENROUTER_API_KEY` overrides `apiKey`
- `--model` overrides `defaultModel`
- `--shell` or `--bash` overrides `defaultShell`

If no API key or model can be resolved, `ai` exits with a setup error.

## Build

Restore and test:

```powershell
dotnet restore tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj
dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore
```

Pack the global tool:

```powershell
dotnet pack src/Ai.Cli/Ai.Cli.csproj --no-restore
```

Install from the local package output:

```powershell
dotnet tool install --global --add-source .\src\Ai.Cli\bin\Release Ai.Cli
```

Update the global tool from this repo:

```powershell
.\scripts\update-tool.ps1
```

The update script stamps each packed build with a new numeric version so `dotnet tool update` can actually move forward. It also installs a managed PowerShell wrapper into your current-user profile and loads it into the current PowerShell session so `ai ...` prompts to execute in-place.

For a first-time global install instead of an update:

```powershell
.\scripts\update-tool.ps1 -Mode Install
```

The managed wrapper script is written to:

- Windows PowerShell or `pwsh`: `%USERPROFILE%\.config\ai\powershell-wrapper.ps1`

The profile integration is only installed for the default global-tool flow. If you use `-ToolPath` for a local/custom install, the wrapper is not added to your profile.

## Usage

Generate a PowerShell command:

```powershell
ai list all files in the current directory, remove everything after the underscore and give me a distinct list
```

In PowerShell, the wrapper prints the generated command and then prompts:

```text
Press Enter to execute, any other key to cancel
```

Press `Enter` to run the command in your current PowerShell session. Press any other key to abandon it. If you want the raw generate-only behavior, call `ai.exe ...` directly.

Generate a bash command wrapper:

```powershell
ai --bash list all markdown files modified in the last day
```

List OpenRouter models:

```powershell
ai --models
```

Print the installed tool version:

```powershell
ai --version
```

Print timing information while generating a command:

```powershell
ai --timing list all files in the current directory
```

`--models`, `--version`, and help output pass straight through without the execution prompt.
