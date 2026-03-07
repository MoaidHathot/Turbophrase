using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class HotkeyBindingTests
{
    [Fact]
    public void HotkeyBinding_DefaultValues_AreEmpty()
    {
        var binding = new HotkeyBinding();

        Assert.Equal(string.Empty, binding.Keys);
        Assert.Equal(string.Empty, binding.Preset);
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
