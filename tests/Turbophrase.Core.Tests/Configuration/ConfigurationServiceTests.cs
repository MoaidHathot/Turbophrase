using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class ConfigurationServiceTests
{
    [Fact]
    public void GetDefaultConfigJson_ReturnsValidJson()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("\"defaultProvider\"", json);
        Assert.Contains("\"providers\"", json);
        Assert.Contains("\"hotkeys\"", json);
        Assert.Contains("\"presets\"", json);
        Assert.Contains("\"customPrompt\"", json);
        Assert.Contains("\"notifications\"", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsOpenAIProvider()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"openai\"", json);
        Assert.Contains("\"type\": \"openai\"", json);
        Assert.Contains("gpt-4o", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsAzureOpenAIProvider()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"azure-openai\"", json);
        Assert.Contains("\"type\": \"azure-openai\"", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsAnthropicProvider()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"anthropic\"", json);
        Assert.Contains("\"type\": \"anthropic\"", json);
        Assert.Contains("claude", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsCopilotProvider()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"copilot\"", json);
        Assert.Contains("\"type\": \"copilot\"", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsOllamaProvider()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"ollama\"", json);
        Assert.Contains("\"type\": \"ollama\"", json);
        Assert.Contains("http://localhost:11434", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsDefaultHotkeys()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("Ctrl+Shift+G", json);
        Assert.Contains("Ctrl+Shift+P", json);
        Assert.Contains("Ctrl+Shift+F", json);
        Assert.Contains("Ctrl+Shift+C", json);
    }

    [Fact]
    public void LoadConfiguration_BindsCustomPromptHotkeyAction()
    {
        var originalPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;

        try
        {
            tempDir = Path.Combine(Path.GetTempPath(), "turbophrase-config-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            var configPath = Path.Combine(tempDir, "turbophrase.json");
            File.WriteAllText(configPath, """
                {
                  "defaultProvider": "openai",
                  "providers": {
                    "openai": {
                      "type": "openai",
                      "apiKey": "test-key",
                      "model": "gpt-4o"
                    }
                  },
                  "hotkeys": [
                    {
                      "keys": "Ctrl+Shift+K",
                      "action": "custom-prompt",
                      "name": "Ask AI",
                      "systemPromptTemplate": "Instruction: {instruction}\nText: {text}",
                      "provider": "openai",
                      "includeInPicker": true,
                      "pickerOrder": 7
                    }
                  ],
                  "pickerActions": [
                    {
                      "action": "custom-prompt",
                      "name": "Picker Prompt",
                      "includeInPicker": true,
                      "pickerOrder": 8
                    }
                  ]
                }
                """);

            ConfigurationService.SetCustomConfigPath(configPath);

            var config = ConfigurationService.LoadConfiguration();
            var binding = Assert.Single(config.Hotkeys);

            Assert.Equal("Ctrl+Shift+K", binding.Keys);
            Assert.Equal("custom-prompt", binding.Action);
            Assert.Equal("Ask AI", binding.Name);
            Assert.Equal("Instruction: {instruction}\nText: {text}", binding.SystemPromptTemplate);
            Assert.Equal("openai", binding.Provider);
            Assert.True(binding.IncludeInPicker);
            Assert.Equal(7, binding.PickerOrder);
            Assert.True(binding.IsCustomPromptAction);

            var pickerAction = Assert.Single(config.PickerActions);
            Assert.Equal("custom-prompt", pickerAction.Action);
            Assert.Equal("Picker Prompt", pickerAction.Name);
            Assert.True(pickerAction.IncludeInPicker);
            Assert.Equal(8, pickerAction.PickerOrder);
        }
        finally
        {
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalPath);

            if (tempDir != null && Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsDefaultPresets()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"grammar\"", json);
        Assert.Contains("\"paraphrase\"", json);
        Assert.Contains("\"formal\"", json);
        Assert.Contains("\"casual\"", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsNotificationSettings()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"showOnStartup\": true", json);
        Assert.Contains("\"showOnSuccess\": true", json);
        Assert.Contains("\"showOnError\": true", json);
        Assert.Contains("\"showOnConfigReload\": true", json);
        Assert.Contains("\"showOnProviderChange\": true", json);
        Assert.Contains("\"showProcessingOverlay\": true", json);
        Assert.Contains("\"showProcessingAnimation\": true", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsCustomPromptTemplate()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("\"customPrompt\"", json);
        Assert.Contains("\"systemPromptTemplate\"", json);
        Assert.Contains("{instruction}", json);
        Assert.Contains("{text}", json);
    }

    [Fact]
    public void GetDefaultConfigJson_ContainsEnvironmentVariableReferences()
    {
        var json = ConfigurationService.GetDefaultConfigJson();

        Assert.Contains("${OPENAI_API_KEY}", json);
        Assert.Contains("${AZURE_OPENAI_ENDPOINT}", json);
        Assert.Contains("${AZURE_OPENAI_KEY}", json);
        Assert.Contains("${ANTHROPIC_API_KEY}", json);
    }

    [Fact]
    public void SetCustomConfigPath_SetsAbsolutePath()
    {
        var originalPath = ConfigurationService.CustomConfigFilePath;
        try
        {
            var customPath = Path.Combine(Path.GetTempPath(), "test-config.json");
            ConfigurationService.SetCustomConfigPath(customPath);

            Assert.Equal(customPath, ConfigurationService.CustomConfigFilePath);
            Assert.Equal(customPath, ConfigurationService.ConfigFilePath);
        }
        finally
        {
            // Reset to original state using reflection (since there's no public reset method)
            if (originalPath == null)
            {
                var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                field?.SetValue(null, null);
            }
        }
    }

    [Fact]
    public void LoadConfiguration_WithNoConfigFile_ReturnsDefaultPresets()
    {
        var originalPath = ConfigurationService.CustomConfigFilePath;
        try
        {
            // Point to a non-existent file
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "turbophrase.json");
            ConfigurationService.SetCustomConfigPath(nonExistentPath);

            var config = ConfigurationService.LoadConfiguration();

            Assert.NotNull(config);
            Assert.NotEmpty(config.Presets);
            Assert.NotEmpty(config.Hotkeys);
            Assert.True(config.Presets.ContainsKey("grammar"));
            Assert.True(config.Presets.ContainsKey("paraphrase"));
            Assert.True(config.Presets.ContainsKey("formal"));
            Assert.True(config.Presets.ContainsKey("casual"));
        }
        finally
        {
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalPath);
        }
    }

    [Fact]
    public void LoadConfiguration_WithNoConfigFile_ReturnsDefaultHotkeys()
    {
        var originalPath = ConfigurationService.CustomConfigFilePath;
        try
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "turbophrase.json");
            ConfigurationService.SetCustomConfigPath(nonExistentPath);

            var config = ConfigurationService.LoadConfiguration();

            Assert.NotNull(config);
            Assert.Equal(4, config.Hotkeys.Count);
            Assert.Contains(config.Hotkeys, h => h.Keys == "Ctrl+Shift+G" && h.Preset == "grammar");
            Assert.Contains(config.Hotkeys, h => h.Keys == "Ctrl+Shift+P" && h.Preset == "paraphrase");
            Assert.Contains(config.Hotkeys, h => h.Keys == "Ctrl+Shift+F" && h.Preset == "formal");
            Assert.Contains(config.Hotkeys, h => h.Keys == "Ctrl+Shift+C" && h.Preset == "casual");
        }
        finally
        {
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalPath);
        }
    }

    [Fact]
    public void XdgConfigDirectory_ReturnsNull_WhenEnvVarNotSet()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            Assert.Null(ConfigurationService.XdgConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
        }
    }

    [Fact]
    public void XdgConfigDirectory_ReturnsNull_WhenEnvVarEmpty()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "");

            Assert.Null(ConfigurationService.XdgConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
        }
    }

    [Fact]
    public void XdgConfigDirectory_ReturnsCombinedPath_WhenEnvVarSet()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            var xdgPath = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdgPath);

            var expected = Path.Combine(xdgPath, "Turbophrase");
            Assert.Equal(expected, ConfigurationService.XdgConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
        }
    }

    [Fact]
    public void ConfigFilePath_UsesXdgPath_WhenEnvVarSetAndConfigExists()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalCustomPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;
        try
        {
            // Clear custom config path so XDG resolution runs
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, null);

            // Create a temp XDG directory with a config file
            tempDir = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            var turbophraseDir = Path.Combine(tempDir, "Turbophrase");
            Directory.CreateDirectory(turbophraseDir);
            File.WriteAllText(Path.Combine(turbophraseDir, "turbophrase.json"), "{}");

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var expectedPath = Path.Combine(turbophraseDir, "turbophrase.json");
            Assert.Equal(expectedPath, ConfigurationService.ConfigFilePath);
            Assert.Equal(turbophraseDir, ConfigurationService.ConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalCustomPath);
            if (tempDir != null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigFilePath_FallsBackToDefault_WhenXdgSetButNoConfigFile()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalCustomPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;
        try
        {
            // Clear custom config path so XDG resolution runs
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, null);

            // Create a temp XDG directory WITHOUT a config file
            tempDir = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            var turbophraseDir = Path.Combine(tempDir, "Turbophrase");
            Directory.CreateDirectory(turbophraseDir);

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            // Should NOT use the XDG path since no config file exists there
            var xdgPath = Path.Combine(turbophraseDir, "turbophrase.json");
            Assert.NotEqual(xdgPath, ConfigurationService.ConfigFilePath);

            // Should fall back to the default %APPDATA% location.
            // If a legacy config already exists there, the resolver intentionally keeps using it.
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var defaultDirectory = Path.Combine(appData, "Turbophrase");
            Assert.Equal(defaultDirectory, ConfigurationService.ConfigDirectory);
            Assert.StartsWith(defaultDirectory, ConfigurationService.ConfigFilePath);
            var resolvedFileName = Path.GetFileName(ConfigurationService.ConfigFilePath);
            Assert.True(resolvedFileName == "turbophrase.json" || resolvedFileName == "config.json");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalCustomPath);
            if (tempDir != null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigFilePath_CustomPathTakesPrecedence_OverXdg()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalCustomPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;
        try
        {
            // Create a temp XDG directory with a config file
            tempDir = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            var turbophraseDir = Path.Combine(tempDir, "Turbophrase");
            Directory.CreateDirectory(turbophraseDir);
            File.WriteAllText(Path.Combine(turbophraseDir, "turbophrase.json"), "{}");

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            // Set a custom config path -- this should take precedence
            var customPath = Path.Combine(Path.GetTempPath(), "custom-config-" + Guid.NewGuid() + ".json");
            ConfigurationService.SetCustomConfigPath(customPath);

            Assert.Equal(customPath, ConfigurationService.ConfigFilePath);
            var xdgPath = Path.Combine(turbophraseDir, "turbophrase.json");
            Assert.NotEqual(xdgPath, ConfigurationService.ConfigFilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalCustomPath);
            if (tempDir != null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadConfiguration_UsesXdgConfig_WhenEnvVarSetAndConfigExists()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalCustomPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;
        try
        {
            // Clear custom config path so XDG resolution runs
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, null);

            // Create a temp XDG directory with a config file that has a custom default provider
            tempDir = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            var turbophraseDir = Path.Combine(tempDir, "Turbophrase");
            Directory.CreateDirectory(turbophraseDir);
            File.WriteAllText(Path.Combine(turbophraseDir, "turbophrase.json"), """
                {
                  "defaultProvider": "anthropic"
                }
                """);

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var config = ConfigurationService.LoadConfiguration();

            Assert.Equal("anthropic", config.DefaultProvider);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalCustomPath);
            if (tempDir != null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigFilePath_UsesLegacyXdgConfig_WhenPreferredFileDoesNotExist()
    {
        var originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalCustomPath = ConfigurationService.CustomConfigFilePath;
        string? tempDir = null;
        try
        {
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, null);

            tempDir = Path.Combine(Path.GetTempPath(), "xdg-test-" + Guid.NewGuid());
            var turbophraseDir = Path.Combine(tempDir, "Turbophrase");
            Directory.CreateDirectory(turbophraseDir);
            File.WriteAllText(Path.Combine(turbophraseDir, "config.json"), "{}");

            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var expectedPath = Path.Combine(turbophraseDir, "config.json");
            Assert.Equal(expectedPath, ConfigurationService.ConfigFilePath);
            Assert.Equal(turbophraseDir, ConfigurationService.ConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
            var field = typeof(ConfigurationService).GetField("_customConfigFilePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, originalCustomPath);
            if (tempDir != null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
