# Winget Package Submission

This directory contains information for submitting Turbophrase to the Windows Package Manager (winget).

## Automatic Manifest Generation

When you create a GitHub release with a tag like `v1.0.0`, the release workflow automatically:

1. Builds x64 and ARM64 versions
2. Creates ZIP archives with SHA256 checksums
3. Generates a winget manifest (`Turbophrase.Turbophrase.yaml`)
4. Attaches all files to the GitHub release

## Submitting to winget-pkgs

1. **Download the manifest** from the GitHub release assets (`Turbophrase.Turbophrase.yaml`)

2. **Fork the winget-pkgs repository**:
   https://github.com/microsoft/winget-pkgs

3. **Create the manifest directory**:
   ```
   manifests/t/Turbophrase/Turbophrase/<version>/
   ```

4. **Split the singleton manifest** into separate files (required by winget-pkgs):
   - `Turbophrase.Turbophrase.yaml` (version manifest)
   - `Turbophrase.Turbophrase.installer.yaml` (installer manifest)
   - `Turbophrase.Turbophrase.locale.en-US.yaml` (locale manifest)

5. **Submit a pull request** to winget-pkgs

## Manual Manifest Creation

If you need to create manifests manually, use the winget-create tool:

```powershell
# Install winget-create
winget install wingetcreate

# Create new manifest
wingetcreate new https://github.com/MoaidHathot/Turbophrase/releases/download/v1.0.0/Turbophrase-1.0.0-win-x64.zip

# Update existing manifest
wingetcreate update Turbophrase.Turbophrase --version 1.0.1 --urls https://github.com/MoaidHathot/Turbophrase/releases/download/v1.0.1/Turbophrase-1.0.1-win-x64.zip
```

## Package Information

- **Package Identifier**: `Turbophrase.Turbophrase`
- **Publisher**: Turbophrase
- **License**: MIT
- **Architectures**: x64, arm64

## Tags

The package uses the following tags for discoverability:
- ai
- text
- productivity
- clipboard
- hotkey
- paraphrase
- prompt
