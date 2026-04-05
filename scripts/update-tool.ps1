[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('Auto', 'Update', 'Install')]
    [string]$Mode = 'Update',

    [string]$Version,

    [string]$ToolPath,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Format-Command {
    param(
        [Parameter(Mandatory)]
        [string[]]$Tokens
    )

    return ($Tokens | ForEach-Object {
            if ($_ -match '\s') {
                '"' + $_.Replace('"', '\"') + '"'
            }
            else {
                $_
            }
        }) -join ' '
}

function Invoke-CommandTokens {
    param(
        [Parameter(Mandatory)]
        [string[]]$Tokens
    )

    & $Tokens[0] $Tokens[1..($Tokens.Count - 1)]
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-BuildVersion {
    param(
        [string]$RequestedVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        return $RequestedVersion
    }

    $baseDate = [DateTime]::SpecifyKind([DateTime]'2000-01-01', [DateTimeKind]::Utc)
    $utcNow = [DateTime]::UtcNow
    $days = [int]($utcNow.Date - $baseDate).TotalDays
    $revision = [int]($utcNow.TimeOfDay.TotalSeconds / 2)

    return "1.0.$days.$revision"
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\Ai.Cli\Ai.Cli.csproj'
$packageSource = Join-Path $repositoryRoot ("src\Ai.Cli\bin\{0}" -f $Configuration)
$packageId = 'Ai.Cli'
$nugetConfigPath = Join-Path $repositoryRoot 'NuGet.Config'
$wrapperInstallerPath = Join-Path $repositoryRoot 'scripts\install-powershell-wrapper.ps1'
$buildVersion = Get-BuildVersion -RequestedVersion $Version

$effectiveMode = $Mode
if ($effectiveMode -eq 'Auto') {
    $effectiveMode = 'Update'
}

[System.Collections.Generic.List[string[]]]$commands = @()
$commands.Add(@(
        'dotnet',
        'pack',
        $projectPath,
        '-c',
        $Configuration,
        '--nologo',
        "/p:Version=$buildVersion",
        "/p:InformationalVersion=$buildVersion"
    ))

$toolLocationTokens = if ([string]::IsNullOrWhiteSpace($ToolPath)) {
    @('--global')
}
else {
    @('--tool-path', $ToolPath)
}

if ($effectiveMode -eq 'Update') {
    $commands.Add(@('dotnet', 'tool', 'update') + $toolLocationTokens + @('--add-source', $packageSource, '--configfile', $nugetConfigPath, '--ignore-failed-sources', $packageId))
}
else {
    $commands.Add(@('dotnet', 'tool', 'install') + $toolLocationTokens + @('--add-source', $packageSource, '--configfile', $nugetConfigPath, '--ignore-failed-sources', $packageId))
}

$shouldInstallPowerShellWrapper = [string]::IsNullOrWhiteSpace($ToolPath)
$wrapperInstallCommand = @(
    '&',
    $wrapperInstallerPath,
    '-LoadInCurrentSession'
)

if ($DryRun) {
    $commands | ForEach-Object { Write-Output (Format-Command -Tokens $_) }
    if ($shouldInstallPowerShellWrapper) {
        Write-Output (Format-Command -Tokens $wrapperInstallCommand)
    }
    exit 0
}

$commands | ForEach-Object { Invoke-CommandTokens -Tokens $_ }

if ($shouldInstallPowerShellWrapper) {
    & $wrapperInstallerPath -LoadInCurrentSession
}
