<#
.SYNOPSIS
    Builds Turbophrase release artifacts.
.DESCRIPTION
    This script builds Turbophrase for both x64 and ARM64 architectures,
    creating ZIP packages for distribution.
.PARAMETER Version
    The version number (default: 1.0.0)
.PARAMETER OutputDir
    The output directory (default: ./artifacts)
.EXAMPLE
    ./build.ps1 -Version 1.2.0
.EXAMPLE
    ./build.ps1 -Version 1.0.0 -OutputDir ./dist
#>
param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "./artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Turbophrase v$Version" -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir" -ForegroundColor Cyan
Write-Host ""

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
Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test src/Turbophrase.slnx --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed"
    exit 1
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
    
    # Create ZIP
    $zipName = "Turbophrase-$Version-$rid.zip"
    Write-Host "Creating $zipName..." -ForegroundColor Cyan
    
    Compress-Archive -Path "$publishDir/*" -DestinationPath "$OutputDir/$zipName" -Force
    
    # Calculate SHA256
    $hash = (Get-FileHash "$OutputDir/$zipName" -Algorithm SHA256).Hash
    Write-Host "SHA256: $hash" -ForegroundColor Gray
    
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
