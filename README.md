# Turbophrase

AI text transformation for Windows. Select text anywhere, press a hotkey, and Turbophrase rewrites it in place using your AI provider.

## Highlights

- **Works from any app**: Notepad, browsers, editors, chat apps, email clients, and more.
- **Fast operation picker**: Press one shortcut, type a few letters, choose the transformation, and continue writing.
- **Custom prompt window**: Capture selected text, type one-off instructions, choose a provider, and run without editing config.
- **Modern settings UI**: Configure providers, presets, hotkeys, picker entries, notifications, startup, and diagnostics visually.
- **Multiple providers**: OpenAI, Azure OpenAI / Foundry, Anthropic, Ollama, and GitHub Copilot.
- **Credential Manager support**: Keep API keys out of plain-text config when you want a safer setup.

## Best Experience

1. Install Turbophrase and start it from the tray.
2. Open **Settings** from the tray menu.
3. Add or test your preferred provider on the **Providers** page.
4. Keep the default hotkeys or adjust them on the **Hotkeys** page.
5. Select text in any app and use the operation picker or a direct preset shortcut.

Most users should not need to edit `turbophrase.json` by hand.

## Install

### GitHub Releases

Download the latest release from [GitHub Releases](https://github.com/MoaidHathot/Turbophrase/releases):

- `Turbophrase-x.x.x-win-x64.zip` for Intel/AMD 64-bit systems
- `Turbophrase-x.x.x-win-arm64.zip` for ARM64 systems

Extract the archive and run `Turbophrase.exe`.

### Winget

```powershell
winget install Turbophrase.Turbophrase
```

## Default Shortcuts

| Hotkey | Action |
|--------|--------|
| `Ctrl+Shift+O` | Open operation picker |
| `Ctrl+Shift+Space` | Open custom prompt |
| `Ctrl+Shift+G` | Fix grammar |
| `Ctrl+Shift+P` | Paraphrase |
| `Ctrl+Shift+F` | Make formal |
| `Ctrl+Shift+C` | Make casual |

Hotkeys are customizable from **Settings -> Hotkeys**.

## Operation Picker

The operation picker is the fastest way to use Turbophrase once you have more than a few transformations.

1. Select text in any app.
2. Press `Ctrl+Shift+O`.
3. Type to filter operations, or use the visible row numbers.
4. Press `Enter` to run the selected operation.
5. Turbophrase replaces the selected text with the result.

The picker includes default presets like grammar, paraphrase, formal, and casual. You can add more presets or picker-only actions from Settings.

## Custom Prompt

Use custom prompt when you want to give one-off instructions.

1. Select text in any app.
2. Press `Ctrl+Shift+Space`.
3. Type an instruction, such as `make this warmer but still concise`.
4. Press `Ctrl+Enter` to run.

Custom prompt shortcuts inside the prompt window:

| Shortcut | Action |
|----------|--------|
| `Ctrl+Enter` | Run prompt |
| `Ctrl+Up` / `Ctrl+Down` | Previous / next provider |
| `Alt+1` through `Alt+9` | Jump to provider by index |
| `Esc` | Cancel |

## Settings UI

Open Settings from the tray icon, or run:

```powershell
Turbophrase.exe settings
```

Settings includes:

- **General**: default provider, startup behavior, custom prompt template
- **Providers**: API keys, endpoints, models, test connection, default provider
- **Presets**: reusable transformations and picker ordering
- **Hotkeys**: global shortcut bindings
- **Operation picker**: curated picker entries and ordering
- **Notifications**: tray/toast/overlay behavior
- **Advanced**: config path, diagnostics, reset tools

## Providers

Turbophrase can use several providers. Configure them in **Settings -> Providers**.

### OpenAI

Use an OpenAI API key and model such as `gpt-4o` or `gpt-4o-mini`.

Environment variable example:

```powershell
setx OPENAI_API_KEY "sk-..."
```

### Azure OpenAI / Foundry

Azure can be configured either with a resource endpoint and deployment name, or with the full Foundry chat-completions URL.

Supported full Foundry endpoint example:

```text
https://your-resource.cognitiveservices.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2025-01-01-preview
```

Turbophrase extracts the resource endpoint and deployment name automatically.

Environment variable examples:

```powershell
setx AZURE_OPENAI_ENDPOINT "https://your-resource.cognitiveservices.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2025-01-01-preview"
setx AZURE_OPENAI_KEY "..."
```

Restart Turbophrase after changing persistent environment variables so the tray process can see them.

### Anthropic

Use an Anthropic API key and model such as `claude-sonnet-4-20250514`.

```powershell
setx ANTHROPIC_API_KEY "sk-ant-..."
```

### Ollama

Use local models through an Ollama endpoint, usually `http://localhost:11434`.

### GitHub Copilot

Uses the bundled GitHub Copilot SDK/CLI integration with your logged-in GitHub Copilot account. No API key is required.

## Presets

Presets are reusable transformations. Turbophrase includes:

- Fix Grammar
- Paraphrase
- Make Formal
- Make Casual

Create your own presets from **Settings -> Presets**. Each preset can:

- use the default provider or a provider override
- appear in the operation picker
- have a custom picker order
- be bound directly to a hotkey

Good preset examples:

- Translate to Spanish
- Summarize
- Shorten
- Make friendlier
- Review tone
- Convert notes to email

## Startup And Notifications

Use **Settings -> General** to run Turbophrase at Windows startup.

Use **Settings -> Notifications** to control:

- startup notification
- success/error notifications
- config reload notifications
- processing overlay
- tray icon animation

## Troubleshooting

### Selected Text Is Not Captured

- Make sure text is selected before pressing the hotkey.
- Try the picker shortcut again after releasing all modifier keys.
- Some apps handle copy shortcuts differently. Turbophrase tries multiple copy methods, but heavily customized editors can still interfere.
- Enable diagnostics from **Settings -> Advanced** if the issue is repeatable.

### Provider Is Not Configured

- Open **Settings -> Providers** and use **Test connection**.
- Check that API keys, endpoints, and deployment names are resolved, not left as `${ENV_VAR}` placeholders.
- Restart Turbophrase after changing persistent environment variables with `setx` or Windows Settings.

### Azure Endpoint Errors

For Azure OpenAI / Foundry, either use:

- resource endpoint: `https://your-resource.openai.azure.com` plus deployment name
- full Foundry endpoint: `https://your-resource.cognitiveservices.azure.com/openai/deployments/<deployment>/chat/completions?...`

## Advanced

Turbophrase stores configuration at `%APPDATA%\Turbophrase\turbophrase.json` by default. The Settings UI is the recommended way to edit it.

Useful CLI commands:

```powershell
Turbophrase.exe settings
Turbophrase.exe config
Turbophrase.exe test [provider-name]
Turbophrase.exe startup --enable
Turbophrase.exe startup --disable
Turbophrase.exe secrets list
Turbophrase.exe secrets set <name>
```

Advanced config routing is still supported for portable or dotfiles workflows, including `--config <path>` and `XDG_CONFIG_HOME`, but those are not needed for the normal experience.

## Building From Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11

### Build

```powershell
dotnet build src/Turbophrase.slnx
dotnet test src/Turbophrase.slnx
```

Create release artifacts:

```powershell
./build.ps1 -Version 1.0.0
```

### Project Structure

```text
src/
  Turbophrase/           # Windows tray app and UI
  Turbophrase.Core/      # Configuration and abstractions
  Turbophrase.Providers/ # AI provider implementations
  Turbophrase.slnx       # Solution file
tests/
  Turbophrase.Core.Tests/
  Turbophrase.Providers.Tests/
```

## License

MIT License - see [LICENSE](LICENSE) for details.

Copyright (c) 2026 Moaid Hathot
