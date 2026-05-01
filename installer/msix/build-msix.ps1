<#
.SYNOPSIS
    Builds Turbophrase MSIX packages for Microsoft Store / sideload.
.DESCRIPTION
    Publishes the Turbophrase app for the requested architectures, then
    packages it into an .msix using makeappx.exe (Windows 10 SDK).

    The Microsoft Store path is:
      1. Reserve the app in Partner Center -> note the Publisher CN and
         Publisher Display Name.
      2. Run `./installer/msix/build-msix.ps1 -StoreSubmission` to produce
         an unsigned package suitable for the Microsoft Store upload.
      3. Upload the .msix or .msixupload to Partner Center; the Store
         signs the package on submission.

    The sideload path is:
      1. Generate or import a code signing cert (self-signed for testing,
         EV cert for distribution).
      2. Run `./installer/msix/build-msix.ps1 -SignCertSubject "CN=My EV"`
         to produce a signed .msix that installs via double-click on
         Windows 10/11.
.PARAMETER Version
    The version. Defaults to Directory.Build.props <Version>.
    Microsoft Store requires four-part versions; we automatically append
    ".0" if the source value is three parts.
.PARAMETER Architectures
    win-x64, win-arm64, or both. Defaults to both.
.PARAMETER OutputDir
    Where to drop .msix files. Defaults to ./artifacts.
.PARAMETER StoreSubmission
    Produce unsigned packages with Identity matching the Partner Center
    reservation (no signing step).
.PARAMETER SignCertSubject
    Subject of an installed code signing cert to sign the package with.
    Mutually exclusive with -StoreSubmission.
.PARAMETER Publisher
    Publisher CN to embed in the manifest. Required for -StoreSubmission;
    inferred from the signing cert otherwise.
.PARAMETER PublisherDisplayName
    Display name shown in Settings > Apps. Defaults to "Moaid Hathot".
.EXAMPLE
    ./installer/msix/build-msix.ps1 -StoreSubmission -Publisher "CN=12345..." -PublisherDisplayName "Moaid Hathot"
.EXAMPLE
    ./installer/msix/build-msix.ps1 -SignCertSubject "CN=Turbophrase Test"
#>
param(
    [string]$Version,
    [string[]]$Architectures = @('win-x64', 'win-arm64'),
    [string]$OutputDir = './artifacts',
    [switch]$StoreSubmission,
    [string]$SignCertSubject,
    [string]$Publisher,
    [string]$PublisherDisplayName = 'Moaid Hathot'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ManifestTemplate = Join-Path $PSScriptRoot 'Package.appxmanifest'
$AssetsDir = Join-Path $PSScriptRoot 'Assets'

function Get-RepoVersion {
    $propsPath = Join-Path $RepoRoot 'Directory.Build.props'
    $xml = [xml](Get-Content -Raw -Path $propsPath)
    return ([string]$xml.Project.PropertyGroup.Version).Trim()
}

function Get-FourPartVersion([string]$value) {
    $parts = $value.Split('.')
    while ($parts.Count -lt 4) {
        $parts += '0'
    }
    return ($parts[0..3] -join '.')
}

function Find-MakeAppx {
    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) { return $null }
    $candidate = Get-ChildItem -Path $sdkRoot -Recurse -Filter 'makeappx.exe' `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($candidate) { return $candidate.FullName }
    return $null
}

function Find-SignTool {
    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) { return $null }
    $candidate = Get-ChildItem -Path $sdkRoot -Recurse -Filter 'signtool.exe' `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($candidate) { return $candidate.FullName }
    return $null
}

function Test-RequiredAssets {
    $required = @(
        'Square44x44Logo.png',
        'Square71x71Logo.png',
        'Square150x150Logo.png',
        'Square310x310Logo.png',
        'Wide310x150Logo.png',
        'SplashScreen.png',
        'StoreLogo.png'
    )

    $missing = @()
    foreach ($asset in $required) {
        if (-not (Test-Path (Join-Path $AssetsDir $asset))) {
            $missing += $asset
        }
    }
    return $missing
}

# --------------------------------------------------------------------
# Pre-flight
# --------------------------------------------------------------------

if (-not $Version) {
    $Version = Get-RepoVersion
}
$FullVersion = Get-FourPartVersion $Version
Write-Host "Building MSIX for v$FullVersion" -ForegroundColor Cyan

if ($StoreSubmission -and $SignCertSubject) {
    throw 'Cannot combine -StoreSubmission with -SignCertSubject. The Store signs the package itself.'
}

if ($StoreSubmission -and -not $Publisher) {
    throw '-Publisher (e.g. "CN=12345678-ABCD-...") is required for Store submissions. Find it in Partner Center.'
}

if (-not $Publisher) {
    if (-not $SignCertSubject) {
        throw 'Either -Publisher or -SignCertSubject must be provided.'
    }
    $Publisher = $SignCertSubject
    Write-Host "Using -Publisher = $Publisher (inferred from signing cert)" -ForegroundColor DarkGray
}

$missingAssets = Test-RequiredAssets
if ($missingAssets.Count -gt 0) {
    Write-Host 'Missing visual assets:' -ForegroundColor Red
    foreach ($asset in $missingAssets) {
        Write-Host "  - $asset"
    }
    Write-Host ''
    Write-Host 'See installer/msix/Assets/README.md for how to generate them.' -ForegroundColor Yellow
    exit 1
}

$makeAppx = Find-MakeAppx
if (-not $makeAppx) {
    throw 'makeappx.exe not found. Install the Windows 10/11 SDK (https://developer.microsoft.com/windows/downloads/windows-sdk).'
}
Write-Host "Using makeappx: $makeAppx" -ForegroundColor DarkGray

$signTool = $null
if ($SignCertSubject) {
    $signTool = Find-SignTool
    if (-not $signTool) {
        throw 'signtool.exe not found alongside makeappx.exe.'
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}
$OutputDir = (Resolve-Path $OutputDir).Path

# --------------------------------------------------------------------
# Per-architecture build
# --------------------------------------------------------------------
foreach ($rid in $Architectures) {
    $arch = $rid.Replace('win-', '')
    $stagingDir = Join-Path $OutputDir "msix-stage-$rid"
    if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
    New-Item -ItemType Directory -Path $stagingDir | Out-Null

    Write-Host ""
    Write-Host "Publishing $rid..." -ForegroundColor Cyan

    # MSIX cannot include single-file bundles -- publish unbundled.
    dotnet publish (Join-Path $RepoRoot 'src/Turbophrase/Turbophrase.csproj') `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:Version=$Version `
        -o $stagingDir

    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }

    # Drop the generated manifest and assets next to Turbophrase.exe.
    $manifest = Get-Content -Raw -Path $ManifestTemplate
    $manifest = $manifest.Replace('__VERSION__', $FullVersion)
    $manifest = $manifest.Replace('__ARCH__', $arch)
    $manifest = $manifest.Replace('__PUBLISHER__', $Publisher)
    $manifest = $manifest.Replace('__PUBLISHERDISPLAY__', $PublisherDisplayName)
    Set-Content -Path (Join-Path $stagingDir 'AppxManifest.xml') -Value $manifest -Encoding UTF8

    $stagingAssets = Join-Path $stagingDir 'Assets'
    if (-not (Test-Path $stagingAssets)) {
        New-Item -ItemType Directory -Path $stagingAssets | Out-Null
    }
    Copy-Item -Path (Join-Path $AssetsDir '*') -Destination $stagingAssets -Recurse -Force `
        -Exclude 'README.md'

    # Pack
    $msixName = "Turbophrase-$Version-$rid.msix"
    $msixPath = Join-Path $OutputDir $msixName
    Write-Host "Packing $msixName..." -ForegroundColor Cyan
    & $makeAppx pack /d $stagingDir /p $msixPath /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed for $rid" }

    if ($SignCertSubject) {
        Write-Host "Signing with cert subject '$SignCertSubject'..." -ForegroundColor Cyan
        & $signTool sign /n $SignCertSubject /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $msixPath
        if ($LASTEXITCODE -ne 0) { throw "signtool failed for $rid" }
    }

    # Hash for the release notes
    $hash = (Get-FileHash $msixPath -Algorithm SHA256).Hash
    Write-Host "  $msixName  SHA256=$hash" -ForegroundColor Gray

    Remove-Item -Recurse -Force $stagingDir
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host 'Artifacts:' -ForegroundColor Cyan
Get-ChildItem $OutputDir -Filter '*.msix' | ForEach-Object { Write-Host "  $($_.Name)" }
