using Anthropic.SDK.Messaging;
using OpenAI.Chat;
using OpenAI.Responses;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// Pure mapping helpers from the provider-agnostic
/// <see cref="ReasoningEffort"/> enum to each provider's native shape.
/// Kept in one place so the test suite can pin down the exact wire
/// behaviour for every value across every provider.
/// </summary>
internal static class ReasoningEffortMapping
{
    /// <summary>
    /// OpenAI Responses API mapping. Returns <c>null</c> when no
    /// <c>reasoning</c> field should be sent (Inherit semantics).
    ///
    /// Clamping:
    /// <list type="bullet">
    /// <item><description><see cref="ReasoningEffort.Off"/> clamps to <c>Minimal</c> (Responses API has no real "off").</description></item>
    /// <item><description><see cref="ReasoningEffort.XHigh"/> clamps to <c>High</c> (Responses API does not expose a higher level).</description></item>
    /// </list>
    /// </summary>
    public static ResponseReasoningEffortLevel? ToOpenAIResponses(ReasoningEffort? effort) => effort switch
    {
        null => (ResponseReasoningEffortLevel?)null,
        ReasoningEffort.Off => ResponseReasoningEffortLevel.Minimal,
        ReasoningEffort.Minimal => ResponseReasoningEffortLevel.Minimal,
        ReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
        ReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
        ReasoningEffort.High => ResponseReasoningEffortLevel.High,
        ReasoningEffort.XHigh => ResponseReasoningEffortLevel.High,
        _ => null,
    };

    /// <summary>
    /// OpenAI Chat Completions mapping (used by the Azure OpenAI provider
    /// since Azure.AI.OpenAI 2.1.0 still pins ChatClient as its primary
    /// surface, but transitive OpenAI 2.10 brings the typed
    /// <see cref="ChatReasoningEffortLevel"/> property).
    /// Same clamping as <see cref="ToOpenAIResponses"/>.
    /// </summary>
    public static ChatReasoningEffortLevel? ToOpenAIChat(ReasoningEffort? effort) => effort switch
    {
        null => (ChatReasoningEffortLevel?)null,
        ReasoningEffort.Off => ChatReasoningEffortLevel.Minimal,
        ReasoningEffort.Minimal => ChatReasoningEffortLevel.Minimal,
        ReasoningEffort.Low => ChatReasoningEffortLevel.Low,
        ReasoningEffort.Medium => ChatReasoningEffortLevel.Medium,
        ReasoningEffort.High => ChatReasoningEffortLevel.High,
        ReasoningEffort.XHigh => ChatReasoningEffortLevel.High,
        _ => null,
    };

    /// <summary>
    /// Anthropic adaptive-thinking effort mapping. Returns <c>null</c>
    /// when no thinking should be enabled (both Inherit and Off, since
    /// Anthropic models default to thinking-off).
    ///
    /// Clamping:
    /// <list type="bullet">
    /// <item><description><see cref="ReasoningEffort.Minimal"/> clamps up to <c>low</c> (Anthropic has no minimal).</description></item>
    /// <item><description><see cref="ReasoningEffort.XHigh"/> maps to <c>max</c>.</description></item>
    /// </list>
    /// </summary>
    public static ThinkingEffort? ToAnthropic(ReasoningEffort? effort) => effort switch
    {
        null => null,
        ReasoningEffort.Off => null,
        ReasoningEffort.Minimal => ThinkingEffort.low,
        ReasoningEffort.Low => ThinkingEffort.low,
        ReasoningEffort.Medium => ThinkingEffort.medium,
        ReasoningEffort.High => ThinkingEffort.high,
        ReasoningEffort.XHigh => ThinkingEffort.max,
        _ => null,
    };

    /// <summary>
    /// Ollama <c>think</c> flag. Returns <c>null</c> for Inherit (omit
    /// the field), <c>false</c> for Off, <c>true</c> for any non-Off
    /// effort. Ollama's wire format is a boolean only; granularity is
    /// handled per-model-server-side.
    /// </summary>
    public static bool? ToOllamaThink(ReasoningEffort? effort) => effort switch
    {
        null => null,
        ReasoningEffort.Off => false,
        _ => true,
    };

    /// <summary>
    /// GitHub Copilot CLI <c>SessionConfig.ReasoningEffort</c> mapping.
    /// Returns <c>null</c> for Inherit and Off (Copilot has no real "off"
    /// — omitting falls through to the CLI's default).
    ///
    /// Clamping:
    /// <list type="bullet">
    /// <item><description><see cref="ReasoningEffort.Minimal"/> clamps up to <c>"low"</c>.</description></item>
    /// <item><description><see cref="ReasoningEffort.XHigh"/> maps to <c>"xhigh"</c>.</description></item>
    /// </list>
    /// </summary>
    public static string? ToCopilot(ReasoningEffort? effort) => effort switch
    {
        null => null,
        ReasoningEffort.Off => null,
        ReasoningEffort.Minimal => "low",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.XHigh => "xhigh",
        _ => null,
    };
}
