# PowerShell Wrapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a PowerShell profile wrapper so `ai ...` prompts for Enter-to-execute in the current PowerShell session while `ai.exe` remains the raw generator.

**Architecture:** Keep the .NET CLI unchanged for generation semantics and layer PowerShell-specific behavior in an installed wrapper function. The update script owns wrapper installation, profile wiring, and immediate session activation.

**Tech Stack:** .NET 10, xUnit, PowerShell 7, existing global tool packaging

---

### Task 1: Cover wrapper installation behavior with tests

**Files:**
- Create: `tests/Ai.Cli.Tests/PowerShellWrapperScriptTests.cs`
- Modify: `tests/Ai.Cli.Tests/UpdateToolScriptTests.cs`

- [ ] **Step 1: Write the failing tests**

Add tests that run the wrapper-install script against temporary paths and assert:

- the wrapper file is written
- the target profile contains a single managed dot-source block
- repeated installs do not duplicate the profile block
- `update-tool.ps1 -DryRun` shows the wrapper-install command

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter PowerShellWrapperScriptTests`
Expected: FAIL because the wrapper installer script and dry-run output do not exist yet

- [ ] **Step 3: Write minimal implementation**

Create a dedicated PowerShell installer script that writes the wrapper and manages the profile block. Update the tool update script to call it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter PowerShellWrapperScriptTests`
Expected: PASS

### Task 2: Cover interactive wrapper execution

**Files:**
- Modify: `tests/Ai.Cli.Tests/PowerShellWrapperScriptTests.cs`
- Create: `scripts/install-powershell-wrapper.ps1`

- [ ] **Step 1: Write the failing tests**

Add tests that install the wrapper against temporary paths with a fake `ai.exe` script and assert:

- generation commands prompt and cancel on non-Enter input
- generation commands execute on Enter input
- `--models` passes through without prompting

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter WrapperFunction`
Expected: FAIL because the wrapper function does not exist yet

- [ ] **Step 3: Write minimal implementation**

Implement the wrapper function in the installer-managed script and resolve the real `ai.exe` explicitly to avoid recursion.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter PowerShellWrapperScriptTests`
Expected: PASS

### Task 3: Document the new shell behavior

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Write the failing documentation expectation**

Review the README and identify missing guidance for:

- `ai.exe` versus PowerShell `ai` wrapper behavior
- profile installation side effects
- how to cancel execution

- [ ] **Step 2: Update the documentation**

Document the installed wrapper, the Enter-to-execute prompt, and the direct `ai.exe` escape hatch.

- [ ] **Step 3: Verify the docs match the implementation**

Run the relevant tests and dry-run commands again, then compare the README examples to actual behavior.

### Task 4: Final verification

**Files:**
- Modify: `scripts/update-tool.ps1`
- Modify: `scripts/install-powershell-wrapper.ps1`
- Modify: `README.md`

- [ ] **Step 1: Run targeted tests**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore --filter PowerShellWrapperScriptTests`
Expected: PASS

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test tests/Ai.Cli.Tests/Ai.Cli.Tests.csproj --no-restore`
Expected: PASS

- [ ] **Step 3: Run update-script dry-run verification**

Run: `pwsh -NoProfile -File .\scripts\update-tool.ps1 -DryRun -Configuration Release`
Expected: output includes pack, global update, and wrapper installation commands
