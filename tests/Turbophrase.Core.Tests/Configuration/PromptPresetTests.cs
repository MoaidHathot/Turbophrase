using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class PromptPresetTests
{
    [Fact]
    public void PromptPreset_DefaultValues_AreEmpty()
    {
        var preset = new PromptPreset();

        Assert.Equal(string.Empty, preset.Name);
        Assert.Equal(string.Empty, preset.SystemPrompt);
        Assert.Null(preset.Provider);
        Assert.True(preset.IncludeInPicker);
        Assert.Null(preset.PickerOrder);
    }

    [Fact]
    public void PromptPreset_CanSetName()
    {
        var preset = new PromptPreset
        {
            Name = "Fix Grammar"
        };

        Assert.Equal("Fix Grammar", preset.Name);
    }

    [Fact]
    public void PromptPreset_CanSetSystemPrompt()
    {
        var preset = new PromptPreset
        {
            SystemPrompt = "Fix all grammar errors in the text."
        };

        Assert.Equal("Fix all grammar errors in the text.", preset.SystemPrompt);
    }

    [Fact]
    public void PromptPreset_CanSetProviderOverride()
    {
        var preset = new PromptPreset
        {
            Name = "Fast Grammar",
            SystemPrompt = "Fix grammar",
            Provider = "anthropic"
        };

        Assert.Equal("anthropic", preset.Provider);
    }

    [Fact]
    public void PromptPreset_NullProvider_UsesDefault()
    {
        var preset = new PromptPreset
        {
            Name = "Grammar",
            SystemPrompt = "Fix grammar",
            Provider = null
        };

        Assert.Null(preset.Provider);
    }

    [Fact]
    public void PromptPreset_CompleteConfiguration()
    {
        var preset = new PromptPreset
        {
            Name = "Professional Rewrite",
            SystemPrompt = "Rewrite the following text in a professional, formal tone suitable for business communication.",
            Provider = "openai"
        };

        Assert.Equal("Professional Rewrite", preset.Name);
        Assert.Contains("professional", preset.SystemPrompt.ToLower());
        Assert.Equal("openai", preset.Provider);
    }
}
