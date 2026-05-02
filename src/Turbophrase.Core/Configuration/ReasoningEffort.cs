using System.Text.Json.Serialization;

namespace Turbophrase.Core.Configuration;

/// <summary>
/// Provider-agnostic reasoning effort level for a transformation. Each
/// provider maps these values to its own native shape (OpenAI's reasoning
/// effort enum, Anthropic's <c>ThinkingEffort</c> on adaptive thinking,
/// Copilot's reasoning_effort string, etc.).
///
/// When the property is <c>null</c> on a <see cref="PromptPreset"/>, the
/// provider's own default is used (no reasoning field is sent in the
/// request).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReasoningEffort>))]
public enum ReasoningEffort
{
    /// <summary>
    /// Explicitly disable reasoning where the provider supports it
    /// (Anthropic: omit thinking; Ollama: <c>think:false</c>). Providers
    /// that have no real "off" (OpenAI, Copilot) clamp to their lowest
    /// available effort.
    /// </summary>
    Off,

    /// <summary>
    /// Lowest reasoning effort. Native on OpenAI/Azure; on
    /// Anthropic and Copilot this clamps up to <c>low</c>.
    /// </summary>
    Minimal,

    /// <summary>
    /// Low reasoning effort.
    /// </summary>
    Low,

    /// <summary>
    /// Medium reasoning effort.
    /// </summary>
    Medium,

    /// <summary>
    /// High reasoning effort.
    /// </summary>
    High,

    /// <summary>
    /// Highest available reasoning effort. Native on Copilot
    /// (<c>"xhigh"</c>) and Anthropic (<c>ThinkingEffort.max</c>); on
    /// OpenAI/Azure this clamps down to <c>High</c>.
    /// </summary>
    XHigh,
}
