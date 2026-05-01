# Microsoft Store and MSIX distribution

This document describes how to ship Turbophrase through the Microsoft Store
or as a sideload-signed MSIX, in addition to the existing winget / Inno
Setup / portable ZIP channels (which continue to work unchanged).

## TL;DR

```powershell
# Sideload (self-signed for testing):
./installer/msix/build-msix.ps1 -SignCertSubject "CN=Turbophrase Test"

# Microsoft Store submission (unsigned -- the Store re-signs):
./installer/msix/build-msix.ps1 -StoreSubmission `
    -Publisher "CN=12345678-ABCD-1234-ABCD-1234567890AB" `
    -PublisherDisplayName "Moaid Hathot"
```

## What changes between unpackaged and packaged builds

The same `Turbophrase.exe` runs in both contexts. At runtime the app
detects its packaging context via
`StartupManager.IsRunningPackaged()` (which calls
`GetCurrentPackageFullName` from kernel32) and dispatches to the right
`IStartupManager` implementation:

| Concern | Unpackaged (winget / Inno / zip) | Packaged (MSIX / Store) |
|---|---|---|
| Run at startup | `HKCU\\...\\Run` registry entry | `Windows.ApplicationModel.StartupTask` (`TurbophraseStartup`) |
| Default config path | `%APPDATA%\\Turbophrase\\turbophrase.json` | `%LOCALAPPDATA%\\Packages\\<PFN>\\LocalCache\\Roaming\\Turbophrase\\turbophrase.json` (Windows redirects `%APPDATA%`) |
| `--config <path>` | Works | Works |
| `XDG_CONFIG_HOME` | Works | Works (env vars are not virtualized) |
| Credential Manager | Works | Works (no extra capability declaration required) |
| Global hotkeys | Works | Works |

So a power user who sets `XDG_CONFIG_HOME` continues to read the same
file regardless of how Turbophrase was installed. The default APPDATA
path is the only thing that moves under MSIX, and the Settings UI's
"Open config folder" button always opens the right directory.

## Prerequisites

1. **Windows 10/11 SDK** -- provides `makeappx.exe` and `signtool.exe`.
   Install via https://developer.microsoft.com/windows/downloads/windows-sdk
   or the Visual Studio individual component "Windows 10 SDK (10.0.22621.0)".
2. **Visual assets** -- generate the PNGs listed in
   `installer/msix/Assets/README.md`. The packaging script will fail fast
   if any are missing.
3. **Partner Center reservation** (Store submissions only) -- reserve the
   product name `Turbophrase` and note the Publisher CN. The script puts
   it into the manifest's `<Identity Publisher="...">` element.
4. **Code-signing certificate** (sideload only) -- self-signed or EV-signed.
   The script signs with `signtool /n <subject>`.

## Step-by-step: Microsoft Store submission

1. **Reserve the app** in
   [Microsoft Partner Center](https://partner.microsoft.com/). Note the
   Publisher CN (looks like `CN=12345678-ABCD-1234-ABCD-1234567890AB`)
   and the package family name.

2. **Generate visual assets** into `installer/msix/Assets/` (see asset
   README). Ideally use Visual Studio's "Generate Image Assets" feature
   pointed at `src/Turbophrase/Resources/Turbophrase.png`.

3. **Build the MSIX**:

   ```powershell
   ./installer/msix/build-msix.ps1 `
       -StoreSubmission `
       -Publisher "CN=<your Publisher ID>" `
       -PublisherDisplayName "Moaid Hathot" `
       -Architectures @('win-x64','win-arm64')
   ```

   This produces `Turbophrase-<version>-win-x64.msix` and
   `Turbophrase-<version>-win-arm64.msix` in `./artifacts`.

4. **Upload to Partner Center**. The Store re-signs the package with its
   own certificate as part of submission. You can upload each MSIX
   individually or bundle them into an `.msixupload`.

5. **Privacy / data-use questionnaire** -- because Turbophrase makes
   outbound calls to OpenAI / Anthropic / Azure / Ollama, declare AI
   usage and link a privacy policy. A minimal policy describing
   "API calls go directly to the provider you configure; Turbophrase
   stores no telemetry" is sufficient.

6. **Submit** -- review typically completes in 24-48 hours.

## Step-by-step: Sideload-signed MSIX

Useful for enterprise pre-deployment or QA before Store submission.

1. **Create a code-signing cert** if you don't have one:

   ```powershell
   New-SelfSignedCertificate `
       -Type Custom `
       -Subject "CN=Turbophrase Test" `
       -KeyUsage DigitalSignature `
       -FriendlyName "Turbophrase Test" `
       -CertStoreLocation "Cert:\CurrentUser\My" `
       -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
   ```

   Then export and trust it on the test machine via
   `Cert:\LocalMachine\TrustedPeople`.

2. **Build the MSIX**:

   ```powershell
   ./installer/msix/build-msix.ps1 `
       -SignCertSubject "CN=Turbophrase Test"
   ```

3. **Install** by double-clicking the resulting `.msix` (the user must
   trust the publisher certificate first).

## Architectural notes for future work

- The current packaging is "MSIX wrapping a self-contained .NET 10 publish".
  We do not yet use the WAP project type (`.wapproj`) because that
  requires Visual Studio / MSBuild SDK installs that don't fit the
  pure `dotnet`/SDK build flow used by `build.ps1` today. The PowerShell
  script is portable and works in CI without VS.
- If you switch to a `.wapproj`, the `Package.appxmanifest` template
  in this folder can be moved verbatim; only the build command changes.
- The Microsoft Store will reject 3-part versions. The script pads with
  `.0`, so `Directory.Build.props` `<Version>1.0.6</Version>` becomes
  `1.0.6.0` in the manifest.

## Verifying packaged behaviour locally

After `build-msix.ps1 -SignCertSubject ...`:

1. Install: `Add-AppxPackage .\artifacts\Turbophrase-<v>-win-x64.msix`.
2. Launch from the Start menu.
3. The Settings tab "Advanced" should show the resolved config path
   under `%LOCALAPPDATA%\Packages\Turbophrase.Turbophrase_...`.
4. Toggle "Run at Windows startup" in the tray menu and confirm the
   prompt mentions the StartupTask (Windows shows a one-time consent
   dialog the first time).
5. Run `turbophrase startup` from a terminal -- the description should
   start with `MSIX StartupTask`.
6. Press `Ctrl+Shift+G` over selected text to confirm hotkeys still
   register under packaged identity.
7. Uninstall: `Get-AppxPackage Turbophrase.Turbophrase | Remove-AppxPackage`.
