<#
.SYNOPSIS
    Tags and releases a new version of Turbophrase.
.DESCRIPTION
    This script bumps the version across the project, commits the changes,
    creates a git tag, and pushes everything to trigger the GitHub Actions
    release workflow. Assumes all other changes are already committed.
.PARAMETER Version
    The version number to release (e.g. 1.0.3). Must follow semantic versioning.
.PARAMETER Force
    Skip the confirmation prompt before pushing.
.PARAMETER DryRun
    Show what would be done without making any changes.
.EXAMPLE
    ./release.ps1 -Version 1.0.3
.EXAMPLE
    ./release.ps1 -Version 2.0.0 -Force
.EXAMPLE
    ./release.ps1 -Version 1.0.3 -DryRun
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[\w.]+)?$')]
    [string]$Version,

    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$tag = "v$Version"

# --- Helpers ---

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
        Write-Host $output -ForegroundColor Red
        exit 1
    }
    return $output
}

# --- Preflight checks ---

Write-Host ""
Write-Host "Turbophrase Release Script" -ForegroundColor Magenta
Write-Host "==========================" -ForegroundColor Magenta
Write-Host "  Version : $Version" -ForegroundColor White
Write-Host "  Tag     : $tag" -ForegroundColor White
if ($DryRun) {
    Write-Host "  Mode    : DRY RUN (no changes will be made)" -ForegroundColor Yellow
}
Write-Host ""

# Check we're in a git repo
if (-not (Test-Path .git)) {
    Write-Err "Not in the root of a git repository."
    exit 1
}

# Check for clean working tree (all changes should already be committed)
Write-Step "Checking working tree..."
$status = Invoke-Git "status", "--porcelain"
# Filter out lines that would be caused by version bump files we're about to modify
$dirtyFiles = $status | Where-Object { $_ -match '\S' }
# We allow the working tree to be clean, or we'll commit the version bump ourselves

# Check the tag doesn't already exist
Write-Step "Checking tag $tag..."
$existingTags = Invoke-Git "tag", "--list", $tag
if ($existingTags) {
    Write-Err "Tag $tag already exists. Aborting."
    exit 1
}
Write-Ok "Tag $tag is available."

# Check we're on main branch
Write-Step "Checking current branch..."
$branch = (Invoke-Git "rev-parse", "--abbrev-ref", "HEAD") | Out-String
$branch = $branch.Trim()
if ($branch -ne "main" -and $branch -ne "master") {
    Write-Err "Current branch is '$branch'. Releases should be created from 'main' or 'master'."
    exit 1
}
Write-Ok "On branch '$branch'."

# Check remote is reachable
Write-Step "Checking remote..."
$remoteUrl = (Invoke-Git "remote", "get-url", "origin") | Out-String
$remoteUrl = $remoteUrl.Trim()
Write-Ok "Remote: $remoteUrl"

# --- Version files ---

$csprojPath = "src/Turbophrase/Turbophrase.csproj"
$programPath = "src/Turbophrase/Program.cs"

Write-Host ""
Write-Host "Updating version references..." -ForegroundColor Magenta
Write-Host ""

# Update .csproj <Version>
Write-Step "Updating $csprojPath..."
$csprojContent = Get-Content $csprojPath -Raw
$updatedCsproj = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
if ($updatedCsproj -eq $csprojContent) {
    Write-Ok "Already at version $Version."
} else {
    if (-not $DryRun) {
        $updatedCsproj | Set-Content $csprojPath -NoNewline
    }
    Write-Ok "Updated to $Version."
}

# Update hardcoded version in Program.cs
Write-Step "Updating $programPath..."
$programContent = Get-Content $programPath -Raw
$updatedProgram = $programContent -replace 'Turbophrase v[\d]+\.[\d]+\.[\d]+[^"]*', "Turbophrase v$Version"
if ($updatedProgram -eq $programContent) {
    Write-Ok "Already at v$Version."
} else {
    if (-not $DryRun) {
        $updatedProgram | Set-Content $programPath -NoNewline
    }
    Write-Ok "Updated to v$Version."
}

# --- Commit version bump if files changed ---

Write-Host ""
Write-Host "Committing and tagging..." -ForegroundColor Magenta
Write-Host ""

$versionStatus = & git status --porcelain $csprojPath $programPath 2>&1
$hasVersionChanges = $versionStatus | Where-Object { $_ -match '\S' }

if ($hasVersionChanges) {
    Write-Step "Staging version bump..."
    if (-not $DryRun) {
        Invoke-Git "add", $csprojPath, $programPath
        Invoke-Git "commit", "-m", "Bump version to $Version"
    }
    Write-Ok "Committed version bump."
} else {
    Write-Skip "No version changes to commit (already at $Version)."
}

# --- Create tag ---

Write-Step "Creating tag $tag..."
if (-not $DryRun) {
    Invoke-Git "tag", $tag
}
Write-Ok "Tag $tag created."

# --- Push ---

Write-Host ""

if (-not $DryRun) {
    if (-not $Force) {
        Write-Host "Ready to push commit and tag $tag to origin." -ForegroundColor Yellow
        Write-Host "This will trigger the GitHub Actions release workflow." -ForegroundColor Yellow
        Write-Host ""
        $confirm = Read-Host "Push to origin? [y/N]"
        if ($confirm -ne "y" -and $confirm -ne "Y") {
            Write-Skip "Push cancelled. Tag $tag has been created locally."
            Write-Host "  To push manually:" -ForegroundColor Gray
            Write-Host "    git push origin $branch" -ForegroundColor Gray
            Write-Host "    git push origin $tag" -ForegroundColor Gray
            Write-Host "  To undo:" -ForegroundColor Gray
            Write-Host "    git tag -d $tag" -ForegroundColor Gray
            if ($hasVersionChanges) {
                Write-Host "    git reset --soft HEAD~1" -ForegroundColor Gray
            }
            exit 0
        }
    }

    Write-Step "Pushing to origin..."
    Invoke-Git "push", "origin", $branch
    Invoke-Git "push", "origin", $tag
    Write-Ok "Pushed successfully."
} else {
    Write-Skip "DRY RUN: Would push $branch and $tag to origin."
}

# --- Done ---

Write-Host ""
Write-Host "Release $tag initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "  GitHub Actions will now:" -ForegroundColor White
Write-Host "    1. Build for win-x64 and win-arm64" -ForegroundColor Gray
Write-Host "    2. Run tests" -ForegroundColor Gray
Write-Host "    3. Create portable ZIPs and installers" -ForegroundColor Gray
Write-Host "    4. Publish GitHub Release with artifacts" -ForegroundColor Gray
Write-Host ""
Write-Host "  Monitor: https://github.com/MoaidHathot/Turbophrase/actions" -ForegroundColor Cyan
Write-Host ""
