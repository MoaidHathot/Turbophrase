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
}
