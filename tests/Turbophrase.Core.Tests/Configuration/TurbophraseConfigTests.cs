using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class TurbophraseConfigTests
{
    [Fact]
    public void TurbophraseConfig_DefaultValues_AreCorrect()
    {
        var config = new TurbophraseConfig();

        Assert.Equal("openai", config.DefaultProvider);
        Assert.NotNull(config.Providers);
        Assert.Empty(config.Providers);
        Assert.NotNull(config.Hotkeys);
        Assert.Empty(config.Hotkeys);
        Assert.NotNull(config.Presets);
        Assert.Empty(config.Presets);
        Assert.NotNull(config.Notifications);
    }

    [Fact]
    public void TurbophraseConfig_CanSetDefaultProvider()
    {
        var config = new TurbophraseConfig
        {
            DefaultProvider = "anthropic"
        };

        Assert.Equal("anthropic", config.DefaultProvider);
    }

    [Fact]
    public void TurbophraseConfig_CanAddProviders()
    {
        var config = new TurbophraseConfig();
        config.Providers["openai"] = new ProviderConfig
        {
            Type = "openai",
            ApiKey = "test-key",
            Model = "gpt-4o"
        };

        Assert.Single(config.Providers);
        Assert.True(config.Providers.ContainsKey("openai"));
        Assert.Equal("openai", config.Providers["openai"].Type);
    }

    [Fact]
    public void TurbophraseConfig_CanAddHotkeys()
    {
        var config = new TurbophraseConfig();
        config.Hotkeys.Add(new HotkeyBinding
        {
            Keys = "Ctrl+Shift+G",
            Preset = "grammar"
        });

        Assert.Single(config.Hotkeys);
        Assert.Equal("Ctrl+Shift+G", config.Hotkeys[0].Keys);
        Assert.Equal("grammar", config.Hotkeys[0].Preset);
    }

    [Fact]
    public void TurbophraseConfig_CanAddPresets()
    {
        var config = new TurbophraseConfig();
        config.Presets["grammar"] = new PromptPreset
        {
            Name = "Fix Grammar",
            SystemPrompt = "Fix grammar errors"
        };

        Assert.Single(config.Presets);
        Assert.True(config.Presets.ContainsKey("grammar"));
        Assert.Equal("Fix Grammar", config.Presets["grammar"].Name);
    }
}

public class NotificationSettingsTests
{
    [Fact]
    public void NotificationSettings_DefaultValues_AreAllTrue()
    {
        var settings = new NotificationSettings();

        Assert.True(settings.ShowOnStartup);
        Assert.True(settings.ShowOnSuccess);
        Assert.True(settings.ShowOnError);
        Assert.True(settings.ShowOnConfigReload);
        Assert.True(settings.ShowOnProviderChange);
        Assert.True(settings.ShowProcessingOverlay);
        Assert.True(settings.ShowProcessingAnimation);
    }

    [Fact]
    public void NotificationSettings_CanDisableNotifications()
    {
        var settings = new NotificationSettings
        {
            ShowOnStartup = false,
            ShowOnSuccess = false,
            ShowOnError = false,
            ShowOnConfigReload = false,
            ShowOnProviderChange = false,
            ShowProcessingOverlay = false,
            ShowProcessingAnimation = false
        };

        Assert.False(settings.ShowOnStartup);
        Assert.False(settings.ShowOnSuccess);
        Assert.False(settings.ShowOnError);
        Assert.False(settings.ShowOnConfigReload);
        Assert.False(settings.ShowOnProviderChange);
        Assert.False(settings.ShowProcessingOverlay);
        Assert.False(settings.ShowProcessingAnimation);
    }
}
