<#
.SYNOPSIS
    Creates and pushes a release tag for Turbophrase.
.DESCRIPTION
    Normalizes the supplied version to a v-prefixed git tag, creates the tag,
    and pushes it to origin to trigger the GitHub Actions release workflow.
    When -Version is omitted, the version from Directory.Build.props is used.
.PARAMETER Version
    The version to release. Accepts 1.0.3 or v1.0.3.
    Defaults to the <Version> in Directory.Build.props (the single source of truth).
.PARAMETER Force
    Skip the confirmation prompt before creating and pushing the tag.
.PARAMETER DryRun
    Show what would be done without making any changes.
.EXAMPLE
    ./release.ps1
.EXAMPLE
    ./release.ps1 -Version 1.0.3 -Force
.EXAMPLE
    ./release.ps1 -DryRun
#>
param(
    [Parameter(Position = 0)]
    [ValidatePattern('^v?\d+\.\d+\.\d+(-[\w.]+)?$')]
    [string]$Version,

    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-RepoVersion {
    $propsPath = Join-Path $PSScriptRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        throw "Directory.Build.props not found at $propsPath"
    }
    $xml = [xml](Get-Content -Raw -Path $propsPath)
    $value = $xml.Project.PropertyGroup.Version
    if (-not $value) {
        throw "<Version> element not found in Directory.Build.props"
    }
    return ([string]$value).Trim()
}

if (-not $Version) {
    $Version = Get-RepoVersion
}

$normalizedVersion = $Version.Trim()
$tag = if ($normalizedVersion.StartsWith("v")) { $normalizedVersion } else { "v$normalizedVersion" }

function Write-Step {
    param([string]$Message)
    Write-Host "  [*] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [+] $Message" -ForegroundColor Green
}

function Write-Skip {
    param([string]$Message)
    Write-Host "  [-] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "  [!] $Message" -ForegroundColor Red
}

function Invoke-Git {
    param([string[]]$Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Err "git $($Arguments -join ' ') failed:"
        if ($output) {
            Write-Host ($output | Out-String).TrimEnd() -ForegroundColor Red
        }
        exit 1
    }

    return $output
}

Write-Host ""
Write-Host "Turbophrase Release Tag Script" -ForegroundColor Magenta
Write-Host "=============================" -ForegroundColor Magenta
Write-Host "  Version : $normalizedVersion" -ForegroundColor White
Write-Host "  Tag     : $tag" -ForegroundColor White
if ($DryRun) {
    Write-Host "  Mode    : DRY RUN (no changes will be made)" -ForegroundColor Yellow
}
Write-Host ""

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Err "git is not installed or not available on PATH."
    exit 1
}

Write-Step "Checking git repository..."
$insideWorkTree = (Invoke-Git "rev-parse", "--is-inside-work-tree" | Out-String).Trim()
if ($insideWorkTree -ne "true") {
    Write-Err "This script must be run inside a git repository."
    exit 1
}
Write-Ok "Inside a git repository."

Write-Step "Checking remote..."
$remoteUrl = (Invoke-Git "remote", "get-url", "origin" | Out-String).Trim()
Write-Ok "Remote: $remoteUrl"

Write-Step "Checking local tag $tag..."
$localTag = (Invoke-Git "tag", "--list", $tag | Out-String).Trim()
if ($localTag) {
    Write-Err "Tag $tag already exists locally."
    exit 1
}
Write-Ok "Tag $tag does not exist locally."

Write-Step "Checking remote tag $tag..."
$remoteTag = (Invoke-Git "ls-remote", "--tags", "origin", "refs/tags/$tag" | Out-String).Trim()
if ($remoteTag) {
    Write-Err "Tag $tag already exists on origin."
    exit 1
}
Write-Ok "Tag $tag does not exist on origin."

Write-Host ""

if (-not $Force) {
    Write-Host "This will create and push tag $tag to origin." -ForegroundColor Yellow
    Write-Host "That push will trigger the GitHub Actions release workflow." -ForegroundColor Yellow
    Write-Host ""

    $confirm = Read-Host "Continue? [y/N]"
    if ($confirm -notin @("y", "Y")) {
        Write-Skip "Cancelled before creating the tag."
        exit 0
    }
}

if ($DryRun) {
    Write-Skip "DRY RUN: Would run 'git tag $tag'"
    Write-Skip "DRY RUN: Would run 'git push origin $tag'"
    exit 0
}

Write-Step "Creating tag $tag..."
Invoke-Git "tag", $tag | Out-Null
Write-Ok "Created tag $tag."

Write-Step "Pushing tag $tag to origin..."
Invoke-Git "push", "origin", $tag | Out-Null
Write-Ok "Pushed tag $tag to origin."

Write-Host ""
Write-Host "Release $tag initiated." -ForegroundColor Green
Write-Host "Monitor: https://github.com/MoaidHathot/Turbophrase/actions" -ForegroundColor Cyan
Write-Host ""
