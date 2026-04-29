using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class HotkeyBindingTests
{
    [Fact]
    public void HotkeyBinding_DefaultValues_AreEmpty()
    {
        var binding = new HotkeyBinding();

        Assert.Equal(string.Empty, binding.Keys);
        Assert.Null(binding.Action);
        Assert.Equal(string.Empty, binding.Preset);
        Assert.Null(binding.Name);
        Assert.Null(binding.SystemPromptTemplate);
        Assert.Null(binding.Provider);
        Assert.False(binding.IncludeInPicker);
        Assert.Null(binding.PickerOrder);
        Assert.True(binding.IsPresetAction);
        Assert.False(binding.IsCustomPromptAction);
        Assert.False(binding.IsPresetPickerAction);
    }

    [Fact]
    public void HotkeyBinding_CanSetKeys()
    {
        var binding = new HotkeyBinding
        {
            Keys = "Ctrl+Shift+G"
        };

        Assert.Equal("Ctrl+Shift+G", binding.Keys);
    }

    [Fact]
    public void HotkeyBinding_CanSetPreset()
    {
        var binding = new HotkeyBinding
        {
            Preset = "grammar"
        };

        Assert.Equal("grammar", binding.Preset);
    }

    [Fact]
    public void HotkeyBinding_CustomPromptAction_IsRecognized()
    {
        var binding = new HotkeyBinding
        {
            Keys = "Ctrl+Shift+K",
            Action = "custom-prompt",
            Name = "Ask AI",
            IncludeInPicker = true,
            PickerOrder = 5
        };

        Assert.True(binding.IsCustomPromptAction);
        Assert.False(binding.IsPresetAction);
        Assert.False(binding.IsPresetPickerAction);
        Assert.Equal("Ask AI", binding.Name);
        Assert.True(binding.IncludeInPicker);
        Assert.Equal(5, binding.PickerOrder);
    }

    [Fact]
    public void HotkeyBinding_PresetPickerAction_IsRecognized()
    {
        var binding = new HotkeyBinding
        {
            Keys = "Ctrl+F7",
            Action = "preset-picker",
            Name = "Choose Operation"
        };

        Assert.True(binding.IsPresetPickerAction);
        Assert.False(binding.IsCustomPromptAction);
        Assert.False(binding.IsPresetAction);
        Assert.Equal("Choose Operation", binding.Name);
    }

    [Fact]
    public void HotkeyBinding_CustomPromptAction_CanSetTemplateAndProvider()
    {
        var binding = new HotkeyBinding
        {
            Keys = "Ctrl+Shift+L",
            Action = "custom-prompt",
            Name = "Summarize",
            SystemPromptTemplate = "Instruction: {instruction}\nText: {text}",
            Provider = "anthropic"
        };

        Assert.Equal("Instruction: {instruction}\nText: {text}", binding.SystemPromptTemplate);
        Assert.Equal("anthropic", binding.Provider);
    }

    [Fact]
    public void HotkeyBinding_CompleteBinding()
    {
        var binding = new HotkeyBinding
        {
            Keys = "Ctrl+Alt+P",
            Preset = "paraphrase"
        };

        Assert.Equal("Ctrl+Alt+P", binding.Keys);
        Assert.Equal("paraphrase", binding.Preset);
    }

    [Theory]
    [InlineData("Ctrl+Shift+G", "grammar")]
    [InlineData("Ctrl+Shift+P", "paraphrase")]
    [InlineData("Ctrl+Shift+F", "formal")]
    [InlineData("Ctrl+Shift+C", "casual")]
    public void HotkeyBinding_DefaultBindings_AreValid(string keys, string preset)
    {
        var binding = new HotkeyBinding
        {
            Keys = keys,
            Preset = preset
        };

        Assert.Equal(keys, binding.Keys);
        Assert.Equal(preset, binding.Preset);
    }
}
