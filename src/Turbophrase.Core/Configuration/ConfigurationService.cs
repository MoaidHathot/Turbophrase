using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Turbophrase.Core.Configuration;

/// <summary>
/// Service for loading and managing Turbophrase configuration.
/// </summary>
public partial class ConfigurationService
{
    private const string AppName = "Turbophrase";
    private const string PreferredConfigFileName = "turbophrase.json";
    private const string LegacyConfigFileName = "config.json";

    private static string? _customConfigFilePath;

    private static readonly Lazy<string> DefaultConfigDirectoryLazy = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppName);
    });

    /// <summary>
    /// Gets the configuration directory path resolved using the fallback chain:
    /// 1. Custom path (--config), 2. XDG_CONFIG_HOME, 3. %APPDATA% default.
    /// </summary>
    public static string ConfigDirectory => _customConfigFilePath != null
        ? Path.GetDirectoryName(_customConfigFilePath) ?? DefaultConfigDirectoryLazy.Value
        : ResolveConfigDirectory();

    /// <summary>
    /// Gets the full path to the configuration file resolved using the fallback chain:
    /// 1. Custom path (--config), 2. XDG_CONFIG_HOME (if set and config exists), 3. %APPDATA% default.
    /// </summary>
    public static string ConfigFilePath => _customConfigFilePath ?? ResolveConfigFilePath();

    /// <summary>
    /// Gets the XDG_CONFIG_HOME-based configuration directory, or null if the variable is not set.
    /// </summary>
    public static string? XdgConfigDirectory
    {
        get
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(xdgConfigHome))
                return null;

            return Path.Combine(xdgConfigHome, AppName);
        }
    }

    /// <summary>
    /// Resolves the configuration directory using XDG_CONFIG_HOME fallback.
    /// Returns the XDG directory if the env var is set and a supported config file exists there,
    /// otherwise returns the default %APPDATA% directory.
    /// </summary>
    private static string ResolveConfigDirectory()
    {
        var xdgDir = XdgConfigDirectory;
        if (xdgDir != null && ResolveExistingConfigFilePath(xdgDir) != null)
        {
            return xdgDir;
        }

        return DefaultConfigDirectoryLazy.Value;
    }

    /// <summary>
    /// Resolves the configuration file path using XDG_CONFIG_HOME fallback.
    /// Returns the XDG config path if the env var is set and a supported file exists there,
    /// otherwise returns the default %APPDATA% config path.
    /// </summary>
    private static string ResolveConfigFilePath()
    {
        var xdgDir = XdgConfigDirectory;
        if (xdgDir != null)
        {
            var xdgConfigPath = ResolveExistingConfigFilePath(xdgDir);
            if (xdgConfigPath != null)
            {
                return xdgConfigPath;
            }
        }

        var defaultConfigPath = ResolveExistingConfigFilePath(DefaultConfigDirectoryLazy.Value);
        return defaultConfigPath ?? Path.Combine(DefaultConfigDirectoryLazy.Value, PreferredConfigFileName);
    }

    private static string? ResolveExistingConfigFilePath(string configDirectory)
    {
        var preferredPath = Path.Combine(configDirectory, PreferredConfigFileName);
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var legacyPath = Path.Combine(configDirectory, LegacyConfigFileName);
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        return null;
    }

    /// <summary>
    /// Sets a custom configuration file path.
    /// </summary>
    /// <param name="configPath">The path to the custom configuration file.</param>
    public static void SetCustomConfigPath(string configPath)
    {
        _customConfigFilePath = Path.GetFullPath(configPath);
    }

    /// <summary>
    /// Gets the custom configuration file path, or null if using default.
    /// </summary>
    public static string? CustomConfigFilePath => _customConfigFilePath;

    /// <summary>
    /// Loads the configuration from the config file and environment variables.
    /// </summary>
    public static TurbophraseConfig LoadConfiguration()
    {
        var builder = new ConfigurationBuilder();

        // Add JSON configuration if file exists
        if (File.Exists(ConfigFilePath))
        {
            builder.AddJsonFile(ConfigFilePath, optional: true, reloadOnChange: false);
        }

        // Add environment variables with prefix
        builder.AddEnvironmentVariables(prefix: "TURBOPHRASE_");

        var configuration = builder.Build();
        var config = new TurbophraseConfig();
        configuration.Bind(config);

        // Process environment variable references in all string properties
        ProcessEnvironmentVariables(config);

        // Apply default presets if none configured
        if (config.Presets.Count == 0)
        {
            config.Presets = GetDefaultPresets();
        }

        // Apply default hotkeys if none configured
        if (config.Hotkeys.Count == 0)
        {
            config.Hotkeys = GetDefaultHotkeys();
        }

        return config;
    }

    /// <summary>
    /// Saves the default provider to the configuration file.
    /// </summary>
    /// <param name="providerName">The provider name to set as default.</param>
    public static void SaveDefaultProvider(string providerName)
    {
        if (!File.Exists(ConfigFilePath))
        {
            throw new FileNotFoundException("Configuration file not found.", ConfigFilePath);
        }

        var jsonContent = File.ReadAllText(ConfigFilePath);
        var jsonNode = JsonNode.Parse(jsonContent) ?? new JsonObject();

        // Update the defaultProvider property (camelCase as per JSON convention)
        jsonNode["defaultProvider"] = providerName;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(ConfigFilePath, jsonNode.ToJsonString(options));
    }

    /// <summary>
    /// Creates the default configuration file if it doesn't exist.
    /// </summary>
    public static void InitializeConfigFile()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        if (!File.Exists(ConfigFilePath))
        {
            File.WriteAllText(ConfigFilePath, GetDefaultConfigJson());
        }
    }

    /// <summary>
    /// Gets the default configuration as JSON.
    /// </summary>
    public static string GetDefaultConfigJson()
    {
        return """
            {
              "defaultProvider": "openai",
              "providers": {
                "openai": {
                  "type": "openai",
                  "apiKey": "${OPENAI_API_KEY}",
                  "model": "gpt-4o"
                },
                "azure-openai": {
                  "type": "azure-openai",
                  "endpoint": "${AZURE_OPENAI_ENDPOINT}",
                  "apiKey": "${AZURE_OPENAI_KEY}",
                  "deploymentName": "gpt-4"
                },
                "anthropic": {
                  "type": "anthropic",
                  "apiKey": "${ANTHROPIC_API_KEY}",
                  "model": "claude-sonnet-4-20250514"
                },
                "copilot": {
                  "type": "copilot",
                  "model": "gpt-4o"
                },
                "ollama": {
                  "type": "ollama",
                  "endpoint": "http://localhost:11434",
                  "model": "llama3.2"
                }
              },
              "hotkeys": [
                { "keys": "Ctrl+Shift+G", "preset": "grammar" },
                { "keys": "Ctrl+Shift+P", "preset": "paraphrase" },
                { "keys": "Ctrl+Shift+F", "preset": "formal" },
                { "keys": "Ctrl+Shift+C", "preset": "casual" }
              ],
              "pickerActions": [],
              "presets": {
                "grammar": {
                  "name": "Fix Grammar",
                  "systemPrompt": "Fix all grammar, spelling, and punctuation errors in the following text. Return ONLY the corrected text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                  "provider": null,
                  "includeInPicker": true,
                  "pickerOrder": 1
                },
                "paraphrase": {
                  "name": "Paraphrase",
                  "systemPrompt": "Paraphrase the following text while maintaining its original meaning. Return ONLY the paraphrased text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                  "provider": null,
                  "includeInPicker": true,
                  "pickerOrder": 2
                },
                "formal": {
                  "name": "Make Formal",
                  "systemPrompt": "Rewrite the following text in a formal, professional tone. Return ONLY the rewritten text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                  "provider": null,
                  "includeInPicker": true,
                  "pickerOrder": 3
                },
                "casual": {
                  "name": "Make Casual",
                  "systemPrompt": "Rewrite the following text in a casual, friendly tone. Return ONLY the rewritten text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                  "provider": null,
                  "includeInPicker": true,
                  "pickerOrder": 4
                }
              },
              "customPrompt": {
                "systemPromptTemplate": "You are a text transformation assistant. Apply the user's instruction to the provided text. Treat the selected text strictly as input text to transform, not as a message to reply to. Return ONLY the transformed text with no explanation or commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.\n\nInstruction:\n{instruction}\n\nText:\n{text}"
              },
              "notifications": {
                "showOnStartup": true,
                "showOnSuccess": true,
                "showOnError": true,
                "showOnConfigReload": true,
                "showOnProviderChange": true,
                "showProcessingOverlay": true,
                "showProcessingAnimation": true
              },
              "logging": {
                "enabled": false
              }
            }
            """;
    }

    private static Dictionary<string, PromptPreset> GetDefaultPresets()
    {
        return new Dictionary<string, PromptPreset>
        {
            ["grammar"] = new PromptPreset
            {
                Name = "Fix Grammar",
                SystemPrompt = "Fix all grammar, spelling, and punctuation errors in the following text. Return ONLY the corrected text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                PickerOrder = 1
            },
            ["paraphrase"] = new PromptPreset
            {
                Name = "Paraphrase",
                SystemPrompt = "Paraphrase the following text while maintaining its original meaning. Return ONLY the paraphrased text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                PickerOrder = 2
            },
            ["formal"] = new PromptPreset
            {
                Name = "Make Formal",
                SystemPrompt = "Rewrite the following text in a formal, professional tone. Return ONLY the rewritten text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                PickerOrder = 3
            },
            ["casual"] = new PromptPreset
            {
                Name = "Make Casual",
                SystemPrompt = "Rewrite the following text in a casual, friendly tone. Return ONLY the rewritten text without any explanation or additional commentary. Do not use em dashes. Use commas, periods, parentheses, or regular hyphens instead.",
                PickerOrder = 4
            }
        };
    }

    private static List<HotkeyBinding> GetDefaultHotkeys()
    {
        return
        [
            new HotkeyBinding { Keys = "Ctrl+Shift+G", Preset = "grammar" },
            new HotkeyBinding { Keys = "Ctrl+Shift+P", Preset = "paraphrase" },
            new HotkeyBinding { Keys = "Ctrl+Shift+F", Preset = "formal" },
            new HotkeyBinding { Keys = "Ctrl+Shift+C", Preset = "casual" }
        ];
    }

    private static void ProcessEnvironmentVariables(TurbophraseConfig config)
    {
        foreach (var provider in config.Providers.Values)
        {
            provider.ApiKey = ResolveEnvironmentVariable(provider.ApiKey);
            provider.Endpoint = ResolveEnvironmentVariable(provider.Endpoint);
            provider.Model = ResolveEnvironmentVariable(provider.Model);
            provider.DeploymentName = ResolveEnvironmentVariable(provider.DeploymentName);
        }
    }

    private static string? ResolveEnvironmentVariable(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Match ${ENV_VAR} pattern
        return EnvVarPattern().Replace(value, match =>
        {
            var envVarName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(envVarName) ?? match.Value;
        });
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex EnvVarPattern();
}
