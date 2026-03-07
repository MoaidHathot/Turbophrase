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
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "config.json");
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
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "config.json");
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
}
