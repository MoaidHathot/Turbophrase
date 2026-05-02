using System.Text.Json;
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
        Assert.Null(preset.ReasoningEffort);
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

    [Fact]
    public void PromptPreset_CanSetReasoningEffort()
    {
        var preset = new PromptPreset
        {
            Name = "Deep think",
            SystemPrompt = "Analyse",
            ReasoningEffort = ReasoningEffort.High,
        };

        Assert.Equal(ReasoningEffort.High, preset.ReasoningEffort);
    }

    [Fact]
    public void PromptPreset_ReasoningEffort_SerializesAsLowercaseString()
    {
        var preset = new PromptPreset
        {
            Name = "x",
            SystemPrompt = "y",
            ReasoningEffort = ReasoningEffort.Medium,
        };

        var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        // Confirm the enum is encoded as a string and field name is camelCased.
        Assert.Contains("\"reasoningEffort\":\"Medium\"", json);
    }

    [Fact]
    public void PromptPreset_ReasoningEffort_OmittedWhenNull()
    {
        var preset = new PromptPreset
        {
            Name = "x",
            SystemPrompt = "y",
            ReasoningEffort = null,
        };

        var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        // Inherit semantics: no reasoningEffort key in the JSON.
        Assert.DoesNotContain("reasoningEffort", json);
    }

    [Theory]
    [InlineData("\"off\"", ReasoningEffort.Off)]
    [InlineData("\"minimal\"", ReasoningEffort.Minimal)]
    [InlineData("\"low\"", ReasoningEffort.Low)]
    [InlineData("\"medium\"", ReasoningEffort.Medium)]
    [InlineData("\"high\"", ReasoningEffort.High)]
    [InlineData("\"xHigh\"", ReasoningEffort.XHigh)]
    [InlineData("\"Off\"", ReasoningEffort.Off)]
    [InlineData("\"HIGH\"", ReasoningEffort.High)]
    public void PromptPreset_ReasoningEffort_DeserializesCaseInsensitive(string jsonValue, ReasoningEffort expected)
    {
        var json = $"{{\"name\":\"x\",\"systemPrompt\":\"y\",\"reasoningEffort\":{jsonValue}}}";
        var preset = JsonSerializer.Deserialize<PromptPreset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(preset);
        Assert.Equal(expected, preset!.ReasoningEffort);
    }
}
