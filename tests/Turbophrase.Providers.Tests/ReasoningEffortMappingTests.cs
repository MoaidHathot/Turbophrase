using Anthropic.SDK.Messaging;
using OpenAI.Chat;
using OpenAI.Responses;
using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

/// <summary>
/// Pins the per-provider mapping for every <see cref="ReasoningEffort"/>
/// value. These tests are the contract that gates the wire-level behaviour
/// and the documented clamp rules.
/// </summary>
public class ReasoningEffortMappingTests
{
    // ============= OpenAI Responses API =============

    [Fact]
    public void OpenAIResponses_Null_ReturnsNull()
    {
        Assert.Null(ReasoningEffortMapping.ToOpenAIResponses(null));
    }

    [Theory]
    [InlineData(ReasoningEffort.Off)]
    [InlineData(ReasoningEffort.Minimal)]
    public void OpenAIResponses_OffAndMinimal_ClampToMinimal(ReasoningEffort effort)
    {
        var result = ReasoningEffortMapping.ToOpenAIResponses(effort);
        Assert.Equal(ResponseReasoningEffortLevel.Minimal, result);
    }

    [Fact]
    public void OpenAIResponses_Low_MapsToLow()
    {
        Assert.Equal(ResponseReasoningEffortLevel.Low, ReasoningEffortMapping.ToOpenAIResponses(ReasoningEffort.Low));
    }

    [Fact]
    public void OpenAIResponses_Medium_MapsToMedium()
    {
        Assert.Equal(ResponseReasoningEffortLevel.Medium, ReasoningEffortMapping.ToOpenAIResponses(ReasoningEffort.Medium));
    }

    [Theory]
    [InlineData(ReasoningEffort.High)]
    [InlineData(ReasoningEffort.XHigh)]
    public void OpenAIResponses_HighAndXHigh_ClampToHigh(ReasoningEffort effort)
    {
        var result = ReasoningEffortMapping.ToOpenAIResponses(effort);
        Assert.Equal(ResponseReasoningEffortLevel.High, result);
    }

    // ============= OpenAI Chat (used by Azure) =============

    [Fact]
    public void OpenAIChat_Null_ReturnsNull()
    {
        Assert.Null(ReasoningEffortMapping.ToOpenAIChat(null));
    }

    [Theory]
    [InlineData(ReasoningEffort.Off)]
    [InlineData(ReasoningEffort.Minimal)]
    public void OpenAIChat_OffAndMinimal_ClampToMinimal(ReasoningEffort effort)
    {
        var result = ReasoningEffortMapping.ToOpenAIChat(effort);
        Assert.Equal(ChatReasoningEffortLevel.Minimal, result);
    }

    [Fact]
    public void OpenAIChat_Low_MapsToLow()
    {
        Assert.Equal(ChatReasoningEffortLevel.Low, ReasoningEffortMapping.ToOpenAIChat(ReasoningEffort.Low));
    }

    [Fact]
    public void OpenAIChat_Medium_MapsToMedium()
    {
        Assert.Equal(ChatReasoningEffortLevel.Medium, ReasoningEffortMapping.ToOpenAIChat(ReasoningEffort.Medium));
    }

    [Theory]
    [InlineData(ReasoningEffort.High)]
    [InlineData(ReasoningEffort.XHigh)]
    public void OpenAIChat_HighAndXHigh_ClampToHigh(ReasoningEffort effort)
    {
        var result = ReasoningEffortMapping.ToOpenAIChat(effort);
        Assert.Equal(ChatReasoningEffortLevel.High, result);
    }

    // ============= Anthropic =============

    [Fact]
    public void Anthropic_Null_ReturnsNull()
    {
        Assert.Null(ReasoningEffortMapping.ToAnthropic(null));
    }

    [Fact]
    public void Anthropic_Off_ReturnsNull()
    {
        // Off means: don't enable thinking at all. Anthropic's adaptive
        // thinking would otherwise waste budget tokens for nothing.
        Assert.Null(ReasoningEffortMapping.ToAnthropic(ReasoningEffort.Off));
    }

    [Theory]
    [InlineData(ReasoningEffort.Minimal)]
    [InlineData(ReasoningEffort.Low)]
    public void Anthropic_MinimalAndLow_ClampToLow(ReasoningEffort effort)
    {
        var result = ReasoningEffortMapping.ToAnthropic(effort);
        Assert.Equal(ThinkingEffort.low, result);
    }

    [Fact]
    public void Anthropic_Medium_MapsToMedium()
    {
        Assert.Equal(ThinkingEffort.medium, ReasoningEffortMapping.ToAnthropic(ReasoningEffort.Medium));
    }

    [Fact]
    public void Anthropic_High_MapsToHigh()
    {
        Assert.Equal(ThinkingEffort.high, ReasoningEffortMapping.ToAnthropic(ReasoningEffort.High));
    }

    [Fact]
    public void Anthropic_XHigh_MapsToMax()
    {
        Assert.Equal(ThinkingEffort.max, ReasoningEffortMapping.ToAnthropic(ReasoningEffort.XHigh));
    }

    // ============= Ollama =============

    [Fact]
    public void Ollama_Null_ReturnsNull()
    {
        Assert.Null(ReasoningEffortMapping.ToOllamaThink(null));
    }

    [Fact]
    public void Ollama_Off_ReturnsFalse()
    {
        Assert.False(ReasoningEffortMapping.ToOllamaThink(ReasoningEffort.Off));
    }

    [Theory]
    [InlineData(ReasoningEffort.Minimal)]
    [InlineData(ReasoningEffort.Low)]
    [InlineData(ReasoningEffort.Medium)]
    [InlineData(ReasoningEffort.High)]
    [InlineData(ReasoningEffort.XHigh)]
    public void Ollama_AnyNonOffEffort_ReturnsTrue(ReasoningEffort effort)
    {
        Assert.True(ReasoningEffortMapping.ToOllamaThink(effort));
    }

    // ============= Copilot =============

    [Fact]
    public void Copilot_Null_ReturnsNull()
    {
        Assert.Null(ReasoningEffortMapping.ToCopilot(null));
    }

    [Fact]
    public void Copilot_Off_ReturnsNull()
    {
        // Off on Copilot omits the field — the CLI's own default applies.
        Assert.Null(ReasoningEffortMapping.ToCopilot(ReasoningEffort.Off));
    }

    [Theory]
    [InlineData(ReasoningEffort.Minimal, "low")]
    [InlineData(ReasoningEffort.Low, "low")]
    [InlineData(ReasoningEffort.Medium, "medium")]
    [InlineData(ReasoningEffort.High, "high")]
    [InlineData(ReasoningEffort.XHigh, "xhigh")]
    public void Copilot_NonOffEfforts_MapToExpectedString(ReasoningEffort effort, string expected)
    {
        Assert.Equal(expected, ReasoningEffortMapping.ToCopilot(effort));
    }

    // ============= Coverage sanity =============

    /// <summary>
    /// Belt-and-braces test that we have a non-throwing mapping for every
    /// enum value across every provider. Catches new enum members that
    /// weren't added to the switch expressions.
    /// </summary>
    [Fact]
    public void AllEnumValues_HaveDefinedMappings()
    {
        foreach (ReasoningEffort effort in Enum.GetValues<ReasoningEffort>())
        {
            // None of these should throw; null is a valid mapping.
            _ = ReasoningEffortMapping.ToOpenAIResponses(effort);
            _ = ReasoningEffortMapping.ToOpenAIChat(effort);
            _ = ReasoningEffortMapping.ToAnthropic(effort);
            _ = ReasoningEffortMapping.ToOllamaThink(effort);
            _ = ReasoningEffortMapping.ToCopilot(effort);
        }
    }
}
