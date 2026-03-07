# Turbophrase

AI-powered text transformation tool for Windows. Select text, press a hotkey, and get AI-enhanced results instantly.

## Features

- **System tray application** - Runs quietly in the background
- **Global hotkeys** - Transform text from any application
- **Multiple AI providers** - OpenAI, Azure OpenAI, Anthropic, Ollama, GitHub Copilot
- **Custom prompts** - Define your own text transformations
- **Runtime provider switching** - Change providers from the tray menu

## Installation

### Download

Download the latest release from [GitHub Releases](https://github.com/MoaidHathot/Turbophrase/releases):

- `Turbophrase-x.x.x-win-x64.zip` - For Intel/AMD 64-bit systems
- `Turbophrase-x.x.x-win-arm64.zip` - For ARM64 systems (Surface Pro X, etc.)

Extract and run `Turbophrase.exe`.

### Winget

```powershell
winget install Turbophrase.Turbophrase
```

## Configuration

On first run, Turbophrase creates a configuration file at `%APPDATA%\Turbophrase\config.json`.

### API Keys

Configure your AI provider by adding your API key to the config file. You can use environment variables:

```json
{
  "providers": {
    "openai": {
      "apiKey": "${OPENAI_API_KEY}",
      "model": "gpt-4o-mini"
    }
  },
  "defaultProvider": "openai"
}
```

### Custom Config Path

Use a custom configuration file:

```powershell
Turbophrase.exe --config "C:\path\to\config.json"
```

Create a default config file at a custom path:

```powershell
Turbophrase.exe --config "C:\path\to\config.json" --init-config
```

## Default Hotkeys

| Hotkey | Action |
|--------|--------|
| `Ctrl+Shift+G` | Fix grammar |
| `Ctrl+Shift+P` | Paraphrase text |
| `Ctrl+Shift+F` | Make formal |
| `Ctrl+Shift+C` | Make casual |

Hotkeys can be customized in the configuration file.

## Startup

Run at Windows startup:

```powershell
# Enable
Turbophrase.exe startup --enable

# Disable
Turbophrase.exe startup --disable

# Check status
Turbophrase.exe startup
```

You can also toggle startup from the tray icon menu.

## Notification Settings

Control which notifications are shown in `config.json`:

```json
{
  "notifications": {
    "showOnStartup": true,
    "showOnSuccess": true,
    "showOnError": true,
    "showOnConfigReload": true,
    "showOnProviderChange": true,
    "showProcessingOverlay": true,
    "showProcessingAnimation": true
  }
}
```

## Supported Providers

### OpenAI

```json
{
  "providers": {
    "openai": {
      "apiKey": "${OPENAI_API_KEY}",
      "model": "gpt-4o-mini"
    }
  }
}
```

### Azure OpenAI

```json
{
  "providers": {
    "azureopenai": {
      "apiKey": "${AZURE_OPENAI_API_KEY}",
      "endpoint": "https://your-resource.openai.azure.com",
      "deploymentName": "gpt-4o-mini"
    }
  }
}
```

### Anthropic

```json
{
  "providers": {
    "anthropic": {
      "apiKey": "${ANTHROPIC_API_KEY}",
      "model": "claude-3-5-sonnet-20241022"
    }
  }
}
```

### Ollama

```json
{
  "providers": {
    "ollama": {
      "endpoint": "http://localhost:11434",
      "model": "llama3.2"
    }
  }
}
```

### GitHub Copilot

Uses your existing GitHub Copilot subscription. Requires the Copilot CLI to be installed and authenticated.

```json
{
  "providers": {
    "copilot": {
      "type": "copilot",
      "model": "gpt-5-mini"
    }
  }
}
```

**Setup:**

1. Install the Copilot CLI via npm:
   ```powershell
   npm install -g @anthropic-ai/copilot-cli
   ```
2. Authenticate with your GitHub account:
   ```powershell
   copilot auth login
   ```
3. No API key required - uses your GitHub Copilot subscription

## CLI Reference

```
turbophrase [options]              Start as system tray application
turbophrase init [options]         Create default configuration file
turbophrase config [options]       Show current configuration
turbophrase test [name] [options]  Test provider connection
turbophrase startup                Show startup registration status
turbophrase startup --enable       Enable run at Windows startup
turbophrase startup --disable      Disable run at Windows startup
turbophrase help                   Show help message
turbophrase version                Show version information
```

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11

### Build

```powershell
# Restore and build
dotnet build src/Turbophrase.slnx

# Run tests
dotnet test src/Turbophrase.slnx

# Create release artifacts
./build.ps1 -Version 1.0.0
```

### Running Tests

The project includes 80 unit tests covering configuration, models, and provider logic:

```powershell
# Run all tests
dotnet test src/Turbophrase.slnx

# Run with detailed output
dotnet test src/Turbophrase.slnx --verbosity normal

# Run specific test project
dotnet test tests/Turbophrase.Core.Tests/Turbophrase.Core.Tests.csproj
dotnet test tests/Turbophrase.Providers.Tests/Turbophrase.Providers.Tests.csproj
```

### Project Structure

```
src/
  Turbophrase/           # Main WinForms tray application
  Turbophrase.Core/      # Core library (configuration, abstractions)
  Turbophrase.Providers/ # AI provider implementations
  Turbophrase.slnx       # Solution file
tests/
  Turbophrase.Core.Tests/      # Configuration and model tests (53 tests)
  Turbophrase.Providers.Tests/ # Provider factory and provider tests (27 tests)
```

## License

MIT License - see [LICENSE](LICENSE) for details.

Copyright (c) 2026 Moaid Hathot
