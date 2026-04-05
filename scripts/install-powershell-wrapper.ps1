[CmdletBinding()]
param(
    [string]$ProfilePath = $PROFILE.CurrentUserCurrentHost,

    [string]$WrapperPath,

    [string]$GeneratorPath,

    [switch]$LoadInCurrentSession
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DefaultWrapperPath {
    if (-not [string]::IsNullOrWhiteSpace($WrapperPath)) {
        return [System.IO.Path]::GetFullPath($WrapperPath)
    }

    $configRoot = if (-not [string]::IsNullOrWhiteSpace($env:XDG_CONFIG_HOME)) {
        $env:XDG_CONFIG_HOME
    }
    elseif ($IsWindows) {
        Join-Path $HOME '.config'
    }
    else {
        Join-Path $HOME '.config'
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Join-Path $configRoot 'ai') 'powershell-wrapper.ps1'))
}

function Resolve-GeneratorPath {
    param(
        [string]$RequestedGeneratorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedGeneratorPath)) {
        return [System.IO.Path]::GetFullPath($RequestedGeneratorPath)
    }

    $commandNames = if ($IsWindows) {
        @('ai.exe', 'ai')
    }
    else {
        @('ai', 'ai.exe')
    }

    foreach ($commandName in $commandNames) {
        $command = Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) {
            return $command.Source
        }
    }

    $globalToolDirectory = Join-Path (Join-Path $HOME '.dotnet') 'tools'
    $candidatePaths = if ($IsWindows) {
        @(
            Join-Path $globalToolDirectory 'ai.exe'
            Join-Path $globalToolDirectory 'ai'
        )
    }
    else {
        @(
            Join-Path $globalToolDirectory 'ai'
            Join-Path $globalToolDirectory 'ai.exe'
        )
    }

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path $candidatePath) {
            return [System.IO.Path]::GetFullPath($candidatePath)
        }
    }

    throw 'Could not resolve the raw ai generator command. Install or update the global tool first.'
}

function Get-WrapperContent {
    param(
        [Parameter(Mandatory)]
        [string]$ResolvedGeneratorPath
    )

    $escapedGeneratorPath = $ResolvedGeneratorPath.Replace("'", "''")

    return @"
Set-StrictMode -Version Latest
`$ErrorActionPreference = 'Stop'

`$script:AiWrapperDefaultGeneratorPath = '$escapedGeneratorPath'
`$script:AiWrapperPassThroughOptions = @('--models', '--version', '-v', '--help', '-h', '-?')

function Resolve-AiWrapperGeneratorPath {
    if (-not [string]::IsNullOrWhiteSpace(`$env:AI_WRAPPER_GENERATOR_PATH)) {
        return `$env:AI_WRAPPER_GENERATOR_PATH
    }

    if (-not [string]::IsNullOrWhiteSpace(`$script:AiWrapperDefaultGeneratorPath) -and (Test-Path `$script:AiWrapperDefaultGeneratorPath)) {
        return `$script:AiWrapperDefaultGeneratorPath
    }

    foreach (`$candidate in @('ai.exe', 'ai')) {
        `$command = Get-Command `$candidate -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (`$null -ne `$command) {
            return `$command.Source
        }
    }

    throw 'Could not resolve the raw ai generator command. Run .\scripts\update-tool.ps1 again or call ai.exe directly.'
}

function Test-AiWrapperPassThrough {
    param(
        [string[]]`$CommandArgs
    )

    if (`$null -eq `$CommandArgs -or `$CommandArgs.Count -eq 0) {
        return `$true
    }

    return [bool](`$CommandArgs | Where-Object { `$script:AiWrapperPassThroughOptions -contains `$_ } | Select-Object -First 1)
}

function Test-AiWrapperPromptSupported {
    if (-not [string]::IsNullOrWhiteSpace(`$env:AI_WRAPPER_DECISION)) {
        return `$true
    }

    if (-not [Environment]::UserInteractive) {
        return `$false
    }

    if ([Console]::IsInputRedirected) {
        return `$false
    }

    if ([Console]::IsOutputRedirected) {
        return `$false
    }

    try {
        `$null = `$Host.UI.RawUI
        return `$true
    }
    catch {
        return `$false
    }
}

function Get-AiWrapperExecuteDecision {
    if (-not [string]::IsNullOrWhiteSpace(`$env:AI_WRAPPER_DECISION)) {
        return [string]::Equals(`$env:AI_WRAPPER_DECISION, 'enter', [StringComparison]::OrdinalIgnoreCase)
    }

    Write-Host 'Press Enter to execute, any other key to cancel'
    `$keyInfo = `$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    Write-Host

    return `$keyInfo.VirtualKeyCode -eq 13
}

function Get-AiWrapperLastExitCode {
    `$lastExitCode = Get-Variable -Name LASTEXITCODE -ErrorAction SilentlyContinue
    if (`$null -eq `$lastExitCode) {
        return 0
    }

    return [int]`$lastExitCode.Value
}

function global:ai {
    [CmdletBinding(PositionalBinding = `$false)]
    param(
        [Parameter(ValueFromRemainingArguments = `$true)]
        [string[]]`$CommandArgs
    )

    `$generatorPath = Resolve-AiWrapperGeneratorPath
    if (Test-AiWrapperPassThrough -CommandArgs `$CommandArgs) {
        & `$generatorPath @CommandArgs
        return
    }

    `$generatedOutput = & `$generatorPath @CommandArgs
    `$exitCode = Get-AiWrapperLastExitCode
    if (`$exitCode -ne 0) {
        `$global:LASTEXITCODE = `$exitCode
        return
    }

    `$generatedCommand = ((@(`$generatedOutput)) -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace(`$generatedCommand)) {
        `$global:LASTEXITCODE = 1
        throw 'The ai generator returned an empty command.'
    }

    Write-Output `$generatedCommand

    if (-not (Test-AiWrapperPromptSupported)) {
        return
    }

    if (-not (Get-AiWrapperExecuteDecision)) {
        Write-Output 'Cancelled.'
        return
    }

    Invoke-Expression `$generatedCommand
}
"@
}

function Set-ManagedProfileBlock {
    param(
        [Parameter(Mandatory)]
        [string]$TargetProfilePath,

        [Parameter(Mandatory)]
        [string]$TargetWrapperPath
    )

    $startMarker = '# >>> ai-pwsh wrapper >>>'
    $endMarker = '# <<< ai-pwsh wrapper <<<'
    $escapedWrapperPath = $TargetWrapperPath.Replace("'", "''")
    $block = @(
        $startMarker
        ". '$escapedWrapperPath'"
        $endMarker
    ) -join [Environment]::NewLine

    $existingContents = if (Test-Path $TargetProfilePath) {
        Get-Content $TargetProfilePath -Raw
    }
    else {
        ''
    }

    $pattern = [regex]::Escape($startMarker) + '.*?' + [regex]::Escape($endMarker)
    if ([regex]::IsMatch($existingContents, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $updatedContents = [regex]::Replace(
            $existingContents,
            $pattern,
            [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $block },
            [System.Text.RegularExpressions.RegexOptions]::Singleline)
    }
    elseif ([string]::IsNullOrWhiteSpace($existingContents)) {
        $updatedContents = $block
    }
    else {
        $separator = if ($existingContents.EndsWith([Environment]::NewLine, [StringComparison]::Ordinal)) {
            [Environment]::NewLine
        }
        else {
            [Environment]::NewLine + [Environment]::NewLine
        }

        $updatedContents = $existingContents + $separator + $block
    }

    Set-Content -Path $TargetProfilePath -Value $updatedContents
}

$resolvedProfilePath = [System.IO.Path]::GetFullPath($ProfilePath)
$resolvedWrapperPath = Get-DefaultWrapperPath
$resolvedGeneratorPath = Resolve-GeneratorPath -RequestedGeneratorPath $GeneratorPath
$wrapperContent = Get-WrapperContent -ResolvedGeneratorPath $resolvedGeneratorPath

$wrapperDirectory = Split-Path -Parent $resolvedWrapperPath
if (-not [string]::IsNullOrWhiteSpace($wrapperDirectory)) {
    New-Item -ItemType Directory -Force -Path $wrapperDirectory | Out-Null
}

$profileDirectory = Split-Path -Parent $resolvedProfilePath
if (-not [string]::IsNullOrWhiteSpace($profileDirectory)) {
    New-Item -ItemType Directory -Force -Path $profileDirectory | Out-Null
}

Set-Content -Path $resolvedWrapperPath -Value $wrapperContent
Set-ManagedProfileBlock -TargetProfilePath $resolvedProfilePath -TargetWrapperPath $resolvedWrapperPath

if ($LoadInCurrentSession) {
    . $resolvedWrapperPath
}
