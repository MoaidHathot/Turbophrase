# MSIX assets

This folder hosts the Microsoft Store / MSIX visual assets referenced from
`Package.appxmanifest`. **The PNGs are not yet committed**; they need to
be generated from the source `Turbophrase.png` / `Turbophrase.ico` artwork
in `src/Turbophrase/Resources/`.

## Required files

| File | Size | Notes |
|------|------|-------|
| `Square44x44Logo.png` | 44 x 44 | Taskbar / start small icon. Provide scaled assets too: `Square44x44Logo.scale-100.png`, `.scale-200.png`, `.scale-400.png`, `.targetsize-16.png`, `.targetsize-24.png`, `.targetsize-48.png`, `.targetsize-256.png`. |
| `Square71x71Logo.png` | 71 x 71 | Small tile. |
| `Square150x150Logo.png` | 150 x 150 | Medium tile. **Required.** |
| `Square310x310Logo.png` | 310 x 310 | Large tile. |
| `Wide310x150Logo.png` | 310 x 150 | Wide tile. |
| `SplashScreen.png` | 620 x 300 | Briefly shown when the user clicks the tile. |
| `StoreLogo.png` | 50 x 50 | Used in the Microsoft Store listing. **Required.** |

## How to generate them

The simplest path is the **Visual Studio "Generate Image Asset"** tool
(right-click `Package.appxmanifest` -> Choose Image Asset). Point it at
`src/Turbophrase/Resources/Turbophrase.png` and let VS produce all
scaled variants into this folder.

If you do not have VS installed, the
[`UWP Image Generator`](https://www.nuget.org/packages/MSIX.NetCore.Tile.Generator)
NuGet tool or [pwa-asset-generator](https://github.com/elegantapp/pwa-asset-generator)
can produce the same set from a single source image.

For convenience, `installer/msix/build-msix.ps1` will fail fast with a
helpful message if any of the required PNGs are missing.
