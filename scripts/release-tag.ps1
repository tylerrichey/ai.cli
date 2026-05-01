[CmdletBinding()]
param(
    [string]$Tag,

    [string]$Remote = 'origin',

    [string]$RepositoryRoot,

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

function Get-GitOutput {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = & git -C $resolvedRepositoryRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed."
    }

    return @($output)
}

function Test-ReleaseTag {
    param(
        [Parameter(Mandatory)]
        [string]$TagName
    )

    return $TagName -match '^v\d+\.\d+\.\d+$'
}

function Get-LatestReleaseTag {
    $tags = @(Get-GitOutput -Arguments @('tag', '--list', 'v*', '--sort=-v:refname'))

    foreach ($candidate in $tags) {
        $trimmedCandidate = $candidate.Trim()
        if (Test-ReleaseTag -TagName $trimmedCandidate) {
            return $trimmedCandidate
        }
    }

    throw 'No release tags found matching v<major>.<minor>.<patch>. Specify -Tag to create one explicitly.'
}

function Get-NextPatchTag {
    param(
        [Parameter(Mandatory)]
        [string]$CurrentTag
    )

    if ($CurrentTag -notmatch '^v(?<Major>\d+)\.(?<Minor>\d+)\.(?<Patch>\d+)$') {
        throw "Tag '$CurrentTag' is not a release tag in v<major>.<minor>.<patch> format."
    }

    $nextPatch = [int]$Matches.Patch + 1
    return "v$($Matches.Major).$($Matches.Minor).$nextPatch"
}

function Test-LocalTagExists {
    param(
        [Parameter(Mandatory)]
        [string]$TagName
    )

    $matches = @(Get-GitOutput -Arguments @('tag', '--list', $TagName))
    return $matches.Count -gt 0
}

$resolvedRepositoryRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    Split-Path -Parent $PSScriptRoot
}
else {
    [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$null = Get-GitOutput -Arguments @('rev-parse', '--git-dir')

$newTag = if ([string]::IsNullOrWhiteSpace($Tag)) {
    Get-NextPatchTag -CurrentTag (Get-LatestReleaseTag)
}
else {
    $Tag.Trim()
}

if (-not (Test-ReleaseTag -TagName $newTag)) {
    throw "Tag '$newTag' must use v<major>.<minor>.<patch> format, such as v1.0.2."
}

if (Test-LocalTagExists -TagName $newTag) {
    throw "Tag '$newTag' already exists locally."
}

$commands = @(
    @('git', '-C', $resolvedRepositoryRoot, 'tag', $newTag),
    @('git', '-C', $resolvedRepositoryRoot, 'push', $Remote, $newTag)
)

if ($DryRun) {
    $commands | ForEach-Object { Write-Output (Format-Command -Tokens $_) }
    exit 0
}

$commands | ForEach-Object { Invoke-CommandTokens -Tokens $_ }
