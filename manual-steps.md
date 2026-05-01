# Turbophrase: Manual Steps

This document is the ordered checklist of human-only steps required to take
Turbophrase from "code complete on disk" (the current state, after PRs #1-#4)
to "running on a non-power user's machine via the Microsoft Store".

Each section is independent. **You can stop after any section** -- shipping
through winget alone is fine; the Store path is additional, not a
replacement.

---

## Section 0 -- Prerequisites you only do once

These are environment requirements; do them before any later section.

1. **.NET 10 SDK** -- already installed if `dotnet --version` works.
2. **Windows 10/11 SDK** -- only required for Section 3 (MSIX packaging).
   Install via the Visual Studio Installer ("Individual components" ->
   "Windows 11 SDK (10.0.22621.0)" or newer), or directly from
   <https://developer.microsoft.com/windows/downloads/windows-sdk>.
   Verify with:
   ```powershell
   Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe | Select-Object -First 1
   ```
3. **Inno Setup 6** -- only required if you keep building the unpackaged
   `.exe` installer. Already a prerequisite of `build.ps1 -BuildInstaller`.
4. **A clean working tree** -- `git status` should be clean before you
   start. Several scripts below produce artifacts that should not be
   committed.

---

## Section 1 -- Smoke-test the new Settings UI locally

Do this **before** committing PRs #1-#4. None of it requires the Store.

### 1.1. Build and run from source

```powershell
cd P:\Github\Turbophrase
dotnet build src/Turbophrase.slnx
dotnet run --project src/Turbophrase/Turbophrase.csproj
```

Expected: Turbophrase tray icon appears in the system tray. If
`turbophrase.json` does not exist or no provider has a usable API key,
the **First-run wizard** opens automatically.

### 1.2. Walk the wizard

1. Pick a provider you actually have credentials for (OpenAI is easiest).
2. Paste your API key. Leave **"Save in Windows Credential Manager"**
   checked (this is the new default).
3. Click Next. The test step should run automatically.
4. Confirm the test reports success in green, then click Finish.

Expected behaviour:
- The wizard closes; the tray icon stays.
- A new entry exists in Credential Manager called
  `Turbophrase:openai:apiKey` (verify in `Control Panel -> Credential
  Manager -> Windows Credentials`).
- `turbophrase.json` shows `"apiKey": "@credman:openai:apiKey"`.

### 1.3. Open Settings and walk every tab

Right-click the tray icon -> **Settings...**. Confirm:

| Tab | What to verify |
|-----|----------------|
| General | Default provider dropdown lists configured providers. "Run at Windows startup" toggle reflects current state. |
| Providers | Your provider is listed and marked `(default)`. The API key field shows `@credman:openai:apiKey` (the reference, not the secret). Click "Test connection" -> green OK. |
| Presets | The four default presets (grammar / paraphrase / formal / casual) are listed. Click one; the system prompt is shown. Add a new preset, save, reopen Settings -- the new preset survives. |
| Hotkeys | The four default hotkeys are listed. Click Edit on one; the `HotkeyCaptureBox` accepts a new combo when you press keys. Cancel the dialog. |
| Operation picker | All presets appear with checkboxes. Move one up/down; save. |
| Notifications | All seven toggles are present. |
| Advanced | "Config file" path matches your actual file. "XDG_CONFIG_HOME" reads `(not set)` unless you set it. "Open config folder" works. |

Click **Save**, then **Close**. Reopen Settings to confirm changes
round-trip. Save with no changes -> button reads "Close" instead of
"Save".

### 1.4. Verify hot reload still works

While Turbophrase is running, hand-edit `turbophrase.json` to flip a
notification toggle. Save the file. Within ~500 ms the tray should
notify "Configuration reloaded" (if you left that toggle on).

### 1.5. Verify a real transformation

1. Select some text in any app (Notepad is fine).
2. Press `Ctrl+Shift+G` (Fix Grammar).
3. Confirm the text gets replaced with the corrected version.

If this works, the integration of the new Settings UI with the existing
hotkey/orchestrator path is correct.

### 1.6. CLI smoke test

```powershell
# In a fresh terminal:
turbophrase config
turbophrase test
turbophrase secrets list
turbophrase secrets set test-key "hello"
turbophrase secrets get test-key
turbophrase secrets remove test-key
turbophrase settings   # opens the Settings window standalone
```

Each should succeed; nothing should crash.

### 1.7. If anything fails

- Capture the failure mode and the contents of `turbophrase.log`
  (enable logging via Settings -> Advanced first).
- File a bug or fix it -- do not proceed to Section 2 until 1.1-1.5 pass.

---

## Section 2 -- Commit and ship via the existing channels (winget + zip)

If you only want to ship the Settings UI and Credential Manager work
through your current channels, this is the last section you need.

### 2.1. Commit

```powershell
git add .
git status   # review the file list
git commit -m "Add Settings UI, Credential Manager support, MSIX scaffolding"
```

**Do not push yet.** First run the existing release pipeline locally
to confirm artifacts still build:

```powershell
./build.ps1 -BuildInstaller
```

Expected output in `./artifacts`:
- `Turbophrase-<version>-win-x64-portable.zip`
- `Turbophrase-<version>-win-arm64-portable.zip`
- `Turbophrase-<version>-win-x64-setup.exe`
- `Turbophrase-<version>-win-arm64-setup.exe`

### 2.2. Bump version (optional, recommended)

Open `Directory.Build.props` and bump `<Version>1.0.6</Version>` to the
new version (e.g. `1.1.0`). Then re-run `./build.ps1 -BuildInstaller`.

### 2.3. Cut a GitHub release

```powershell
git tag v<new-version>
git push origin main --tags
gh release create v<new-version> ./artifacts/* --generate-notes
```

### 2.4. Update the winget manifest

Use `./release.ps1` (already in the repo) or follow the existing
`winget/README.md` instructions. The Settings UI work doesn't change
the winget package shape; only `Version` and `InstallerSha256` need
updating.

After `winget upgrade Turbophrase.Turbophrase` shows the new version,
Section 2 is complete.

---

## Section 3 -- Ship via the Microsoft Store

Only do this section if you actually want a Store listing. None of the
later steps affect the existing winget channel.

### 3.1. Register as a Microsoft developer

1. Go to <https://partner.microsoft.com/dashboard>.
2. Sign up for a developer account (~$19 USD, one-time).
3. Complete the identity verification (driver's licence + phone).

### 3.2. Reserve the app name

1. In Partner Center, click **Apps and games -> New product -> MSIX or PWA app**.
2. Reserve the name `Turbophrase`.
3. Open the new app's **Product identity** page and copy two values:
   - **Publisher** (looks like `CN=12345678-ABCD-1234-ABCD-1234567890AB`)
   - **Publisher display name** (the friendly name shown to users)

### 3.3. Generate visual assets

The packaging script will refuse to run until these PNGs exist:

| File | Size |
|------|------|
| `installer/msix/Assets/Square44x44Logo.png` | 44 x 44 |
| `installer/msix/Assets/Square71x71Logo.png` | 71 x 71 |
| `installer/msix/Assets/Square150x150Logo.png` | 150 x 150 |
| `installer/msix/Assets/Square310x310Logo.png` | 310 x 310 |
| `installer/msix/Assets/Wide310x150Logo.png` | 310 x 150 |
| `installer/msix/Assets/SplashScreen.png` | 620 x 300 |
| `installer/msix/Assets/StoreLogo.png` | 50 x 50 |

The simplest way to generate them is the Visual Studio "Generate Image
Assets" tool (right-click `Package.appxmanifest` -> "Choose Image
Asset", point at `src/Turbophrase/Resources/Turbophrase.png`). VS
creates all sizes plus the scaled variants (`.scale-100.png`,
`.scale-200.png`, etc.) for free.

If you don't have VS, use any of:
- <https://github.com/elegantapp/pwa-asset-generator>
- The MSIX Toolkit "Image Generator" (NuGet)
- Any image editor with a batch-resize action

Drop the generated files into `installer/msix/Assets/`. Do **not**
commit the existing `Assets/README.md` over.

### 3.4. Sideload-test the MSIX before submitting

Strongly recommended. This catches manifest errors and proves the
packaged StartupTask path works on a real machine.

1. Create a self-signed test certificate:

   ```powershell
   $cert = New-SelfSignedCertificate `
       -Type Custom `
       -Subject "CN=Turbophrase Test" `
       -KeyUsage DigitalSignature `
       -FriendlyName "Turbophrase Test" `
       -CertStoreLocation "Cert:\CurrentUser\My" `
       -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
   ```

2. Export the public part and import it into Trusted People so Windows
   accepts the signature:

   ```powershell
   $pwd = ConvertTo-SecureString -String "test" -Force -AsPlainText
   Export-PfxCertificate -Cert $cert -FilePath .\turbophrase-test.pfx -Password $pwd
   Import-Certificate -FilePath .\turbophrase-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   (You may need to run the second command from an elevated shell.)

3. Build a signed MSIX:

   ```powershell
   ./installer/msix/build-msix.ps1 -SignCertSubject "CN=Turbophrase Test"
   ```

   Expected output: `./artifacts/Turbophrase-<version>-win-x64.msix`
   and the arm64 variant.

4. Install:

   ```powershell
   Add-AppxPackage .\artifacts\Turbophrase-<version>-win-x64.msix
   ```

5. Smoke-test under packaged identity:
   - Launch from the Start menu (the unpackaged exe is no longer the
     primary one if it's also installed; uninstall it first to avoid
     confusion).
   - Verify the tray icon appears.
   - Open Settings -> Advanced. The "Config file" path should now point
     under `%LOCALAPPDATA%\Packages\Turbophrase.Turbophrase_<hash>\
     LocalCache\Roaming\Turbophrase\turbophrase.json`.
   - In the tray menu, toggle "Run at Windows startup". Windows should
     show a one-time consent dialog mentioning the StartupTask. Accept.
   - In a terminal, run `turbophrase startup`. The output should start
     with `MSIX StartupTask 'TurbophraseStartup' state=Enabled`.
   - Press `Ctrl+Shift+G` over selected text -- hotkeys must still
     register and trigger transformations under packaged identity.
   - Set `XDG_CONFIG_HOME` and restart -- verify the Advanced tab now
     reads the XDG-rooted file.

6. Uninstall when done:

   ```powershell
   Get-AppxPackage Turbophrase.Turbophrase | Remove-AppxPackage
   ```

If anything in step 5 fails, fix it before submitting to the Store --
Store review will hit the same issues but with weeks of feedback delay.

### 3.5. Build the Store submission package

```powershell
./installer/msix/build-msix.ps1 `
    -StoreSubmission `
    -Publisher "CN=<your Publisher ID from Partner Center>" `
    -PublisherDisplayName "<your Publisher display name>"
```

Expected output: unsigned `.msix` files in `./artifacts`. The Store
re-signs them on submission.

### 3.6. Fill out the Partner Center submission

In Partner Center, on your reserved app:

1. **Pricing and availability** -- Free; choose your markets; choose
   "Available" or "Hidden" depending on whether you want unlisted.
2. **Properties** -- Category: `Productivity`. Subcategory:
   `Personal finance` or `Notes & organizers` (closest fits).
3. **Age rating** -- Fill the IARC questionnaire honestly. Turbophrase
   has no UGC, no in-app purchase; it should rate as 3+ in all regions.
4. **Packages** -- upload both `.msix` files (or bundle them into an
   `.msixupload`). The dashboard will validate them and show any
   manifest errors. Common issues:
   - "Identity Publisher does not match" -> you used the wrong Publisher
     CN. Re-run the build with the correct value.
   - "Version too low" -> bump `Directory.Build.props` and rebuild.
5. **Store listings** -- name, short description (200 char max), long
   description, screenshots (at least one 1366x768 or larger). Take
   screenshots of:
   - The tray menu open
   - The Settings window with the Providers tab
   - A before/after of a text transformation
6. **Privacy policy URL** -- required because Turbophrase makes
   outbound calls to AI services. A minimal page on your own site
   stating "Turbophrase sends the text you select to the provider you
   configure (OpenAI / Anthropic / etc.). Turbophrase itself stores no
   telemetry, no user data, and no analytics." is sufficient.
7. **AI declaration** -- in the "Properties" page, declare AI usage.
   Be specific that the AI provider is user-configurable.
8. **Submit**.

### 3.7. Wait for review

Microsoft reviews typically complete in 24-72 hours for desktop apps.
You'll get an email if anything is rejected; common reasons:

- Missing privacy policy link
- Screenshots that don't show the actual product
- AI usage not declared

If approved, your app appears at
`https://apps.microsoft.com/detail/<storeid>` and is installable via:

```powershell
winget install --source msstore Turbophrase
```

(Yes, both Microsoft Store and `winget` channels can serve the same
product. winget will start preferring the Store source automatically.)

### 3.8. Tagging the release on GitHub

Once the Store accepts the build, mirror the same artifacts on GitHub:

```powershell
git tag v<version>-store
git push origin main --tags
gh release create v<version>-store ./artifacts/Turbophrase-*.msix ./artifacts/Turbophrase-*-portable.zip --generate-notes
```

This keeps the unpackaged channel and the Store channel in sync.

---

## Section 4 -- Ongoing maintenance

Things to remember after the initial submission.

### 4.1. Every release

1. Update `Directory.Build.props` `<Version>`.
2. `./build.ps1 -BuildInstaller` (unpackaged channel).
3. `./installer/msix/build-msix.ps1 -StoreSubmission -Publisher "CN=..." -PublisherDisplayName "..."` (Store).
4. Cut a git tag and a GitHub release.
5. Submit the new MSIX to Partner Center.
6. Update the winget manifest.

### 4.2. If a user reports "my keys disappeared after upgrading"

That means the `@credman:` reference in their config points at a
secret the new install can't read. Likely cause: they reinstalled with
"clean configuration" and the Credential Manager entry was deleted by
the uninstaller. Recovery:

1. Walk them through Settings -> Providers -> paste their key again,
   leave "Save in Credential Manager" checked, save.

### 4.3. If you ever need to migrate away from `@credman:`

The references are just strings in JSON. Replace `"@credman:openai:apiKey"`
with the literal key value (or `"${OPENAI_API_KEY}"`) and Turbophrase
keeps working. There's no lock-in.

---

## Quick reference

| Goal | Command |
|------|---------|
| Build everything | `dotnet build src/Turbophrase.slnx` |
| Run tests | `dotnet test src/Turbophrase.slnx` |
| Run from source | `dotnet run --project src/Turbophrase` |
| Build unpackaged release artifacts | `./build.ps1 -BuildInstaller` |
| Build sideload-signed MSIX | `./installer/msix/build-msix.ps1 -SignCertSubject "CN=Turbophrase Test"` |
| Build Store-submission MSIX | `./installer/msix/build-msix.ps1 -StoreSubmission -Publisher "CN=..." -PublisherDisplayName "..."` |
| Open Settings UI | tray menu -> Settings, or `turbophrase settings` |
| List stored secrets | `turbophrase secrets list` |
| Save a secret | `turbophrase secrets set <name>` (paste, hidden) |
