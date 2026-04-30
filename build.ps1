<#
.SYNOPSIS
    Builds Turbophrase release artifacts.
.DESCRIPTION
    This script builds Turbophrase for both x64 and ARM64 architectures,
    creating ZIP packages (portable) and optionally installers for distribution.
    Requires .NET 10.0 SDK. Inno Setup is required for building installers.
.PARAMETER Version
    The version number. Defaults to the value in Directory.Build.props,
    which is the single source of truth for the product version.
.PARAMETER OutputDir
    The output directory (default: ./artifacts)
.PARAMETER SkipTests
    Skip running tests before building
.PARAMETER BuildInstaller
    Build Inno Setup installer in addition to portable ZIP
.EXAMPLE
    ./build.ps1
.EXAMPLE
    ./build.ps1 -Version 1.2.0 -OutputDir ./dist
.EXAMPLE
    ./build.ps1 -SkipTests
.EXAMPLE
    ./build.ps1 -BuildInstaller
#>
param(
    [string]$Version,
    [string]$OutputDir = "./artifacts",
    [switch]$SkipTests,
    [switch]$BuildInstaller
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

Write-Host "Building Turbophrase v$Version" -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir" -ForegroundColor Cyan
if ($BuildInstaller) {
    Write-Host "Installer build: Enabled" -ForegroundColor Cyan
}
Write-Host ""

# Check .NET SDK version
$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Gray

# Check for Inno Setup if building installer
$isccPath = $null
if ($BuildInstaller) {
    $isccPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $isccPaths) {
        if (Test-Path $path) {
            $isccPath = $path
            break
        }
    }
    if (-not $isccPath) {
        Write-Error "Inno Setup not found. Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php"
        exit 1
    }
    Write-Host "Using Inno Setup: $isccPath" -ForegroundColor Gray
}

# Clean output directory
if (Test-Path $OutputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore src/Turbophrase.slnx
if ($LASTEXITCODE -ne 0) {
    Write-Error "Restore failed"
    exit 1
}

# Run tests
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test src/Turbophrase.slnx --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit 1
    }
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host "Skipping tests (--SkipTests specified)" -ForegroundColor Yellow
}

$runtimes = @("win-x64", "win-arm64")

foreach ($rid in $runtimes) {
    Write-Host ""
    Write-Host "Building for $rid..." -ForegroundColor Cyan
    
    $publishDir = "$OutputDir/publish-$rid"
    
    dotnet publish src/Turbophrase/Turbophrase.csproj `
        -c Release `
        -r $rid `
        -p:Version=$Version `
        -o $publishDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $rid"
        exit 1
    }
    
    # Create ZIP (Portable)
    $zipName = "Turbophrase-$Version-$rid-portable.zip"
    Write-Host "Creating $zipName..." -ForegroundColor Cyan
    
    Compress-Archive -Path "$publishDir/*" -DestinationPath "$OutputDir/$zipName" -Force
    
    # Calculate SHA256 for ZIP
    $hash = (Get-FileHash "$OutputDir/$zipName" -Algorithm SHA256).Hash
    Write-Host "SHA256: $hash" -ForegroundColor Gray
    
    # Build installer if requested
    if ($BuildInstaller) {
        $arch = $rid.Replace("win-", "")
        $installerName = "Turbophrase-$Version-$rid-setup"
        Write-Host "Creating $installerName.exe..." -ForegroundColor Cyan
        
        $repoRoot = (Get-Location).Path
        $fullPublishDir = (Resolve-Path $publishDir).Path
        $fullOutputDir = (Resolve-Path $OutputDir).Path
        
        & $isccPath `
            /DVersion="$Version" `
            /DArchitecture="$arch" `
            /DSourcePath="$fullPublishDir" `
            /DRepoRoot="$repoRoot" `
            /O"$fullOutputDir" `
            /F"$installerName" `
            /Q `
            installer/Turbophase.iss
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Installer build failed for $rid"
            exit 1
        }
        
        # Calculate SHA256 for installer
        $installerHash = (Get-FileHash "$OutputDir/$installerName.exe" -Algorithm SHA256).Hash
        Write-Host "SHA256: $installerHash" -ForegroundColor Gray
    }
    
    # Clean up publish directory
    Remove-Item -Recurse -Force $publishDir
}

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Artifacts:" -ForegroundColor Cyan
Get-ChildItem $OutputDir | ForEach-Object {
    Write-Host "  $($_.Name)" -ForegroundColor White
}
