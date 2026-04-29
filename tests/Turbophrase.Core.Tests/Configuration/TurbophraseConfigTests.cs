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
        Assert.NotNull(config.PickerActions);
        Assert.Empty(config.PickerActions);
        Assert.NotNull(config.Presets);
        Assert.Empty(config.Presets);
        Assert.NotNull(config.Notifications);
        Assert.NotNull(config.CustomPrompt);
        Assert.Contains("{instruction}", config.CustomPrompt.SystemPromptTemplate);
        Assert.Contains("{text}", config.CustomPrompt.SystemPromptTemplate);
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
    public void TurbophraseConfig_CanAddPickerActions()
    {
        var config = new TurbophraseConfig();
        config.PickerActions.Add(new HotkeyBinding
        {
            Action = "custom-prompt",
            Name = "Custom Prompt",
            IncludeInPicker = true,
            PickerOrder = 10
        });

        var action = Assert.Single(config.PickerActions);
        Assert.Equal("custom-prompt", action.Action);
        Assert.Equal("Custom Prompt", action.Name);
        Assert.True(action.IncludeInPicker);
        Assert.Equal(10, action.PickerOrder);
    }

    [Fact]
    public void TurbophraseConfig_CanAddPresets()
    {
        var config = new TurbophraseConfig();
        config.Presets["grammar"] = new PromptPreset
        {
            Name = "Fix Grammar",
            SystemPrompt = "Fix grammar errors",
            IncludeInPicker = false,
            PickerOrder = 2
        };

        Assert.Single(config.Presets);
        Assert.True(config.Presets.ContainsKey("grammar"));
        Assert.Equal("Fix Grammar", config.Presets["grammar"].Name);
        Assert.False(config.Presets["grammar"].IncludeInPicker);
        Assert.Equal(2, config.Presets["grammar"].PickerOrder);
    }

    [Fact]
    public void TurbophraseConfig_CanSetCustomPromptTemplate()
    {
        var config = new TurbophraseConfig
        {
            CustomPrompt = new CustomPromptSettings
            {
                SystemPromptTemplate = "Instruction: {instruction}\nText: {text}"
            }
        };

        Assert.Equal("Instruction: {instruction}\nText: {text}", config.CustomPrompt.SystemPromptTemplate);
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
