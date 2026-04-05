# PowerShell Wrapper Design

**Date:** 2026-03-23

## Goal

Make `ai ...` feel native in PowerShell by prompting to execute the generated command in the user's current shell session, while keeping the .NET global tool as the raw cross-platform generator.

## Constraints

- The packaged `ai.exe` process cannot inject execution back into the caller's already-running PowerShell scope.
- The current clipboard behavior should remain.
- `--models`, `--version`, and help flows should stay non-interactive.
- Existing global tool installation and update flow should remain the entrypoint.

## Design

### Raw generator stays in .NET

The .NET tool keeps responsibility for:

- generating commands from OpenRouter
- printing the generated command
- copying it to the clipboard
- listing models
- printing version/timing output

This preserves the current cross-platform contract and keeps non-PowerShell shells usable through `ai.exe`.

### PowerShell owns interactive execution

A PowerShell wrapper function named `ai` will be installed into the user's profile. The wrapper will:

1. resolve the real `ai.exe`
2. pass through informational commands like `--models`, `--version`, and help
3. capture generation output from `ai.exe`
4. print the generated command back to the console
5. prompt `Press Enter to execute, any other key to cancel`
6. execute the command in the current PowerShell session when Enter is pressed

This keeps execution in the user's active session and current working directory rather than a child process.

### Installation flow

`scripts/update-tool.ps1` will install or update the wrapper after packing/updating the tool. The script will:

- write a managed wrapper script under the user's `~/.config/ai` directory
- ensure the current PowerShell profile dot-sources that wrapper script
- load the wrapper into the current session so the new behavior is immediately available

The managed profile block must be idempotent so repeated updates do not duplicate entries.

## Non-goals

- executing commands automatically without confirmation
- making `cmd.exe` or bash sessions interactive in the same way
- changing the raw `ai.exe` contract for non-PowerShell callers
