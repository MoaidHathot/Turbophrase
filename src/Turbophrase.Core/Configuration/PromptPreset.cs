namespace Turbophrase.Core.Configuration;

/// <summary>
/// A named preset with a system prompt and optional provider override.
/// </summary>
public class PromptPreset
{
    /// <summary>
    /// Display name for the preset.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The system prompt to use for text transformation.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Optional provider name to use instead of the default.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Whether this preset should appear in the operation picker.
    /// Presets are included by default.
    /// </summary>
    public bool IncludeInPicker { get; set; } = true;

    /// <summary>
    /// Optional sort order in the operation picker. Lower values appear first.
    /// </summary>
    public int? PickerOrder { get; set; }

    /// <summary>
    /// Optional reasoning effort override forwarded to the provider on each
    /// request. <c>null</c> means "no opinion": the provider sends no
    /// reasoning-related field and the underlying API uses its own default.
    ///
    /// Each provider maps this enum to its native shape; providers that do
    /// not support reasoning silently ignore it. See
    /// <see cref="ReasoningEffort"/> for per-value semantics and per-provider
    /// clamping behaviour.
    /// </summary>
    public ReasoningEffort? ReasoningEffort { get; set; }
}
