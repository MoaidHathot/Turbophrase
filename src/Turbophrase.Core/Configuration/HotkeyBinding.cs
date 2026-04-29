namespace Turbophrase.Core.Configuration;

/// <summary>
/// Binding between a keyboard shortcut and an action.
/// </summary>
public class HotkeyBinding
{
    /// <summary>
    /// The keyboard shortcut (e.g., "Ctrl+Shift+G").
    /// </summary>
    public string Keys { get; set; } = string.Empty;

    /// <summary>
    /// The action to execute when this hotkey is pressed.
    /// If omitted, the binding is treated as a preset binding for backward compatibility.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The preset name to execute when this hotkey is pressed.
    /// Used when <see cref="Action"/> is omitted or set to <c>preset</c>.
    /// </summary>
    public string Preset { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name for non-preset actions.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional system prompt template for custom prompt actions.
    /// Supports the placeholders {instruction} and {text}.
    /// </summary>
    public string? SystemPromptTemplate { get; set; }

    /// <summary>
    /// Optional provider override for custom prompt actions.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Whether this non-preset action should appear in the operation picker.
    /// Actions are excluded by default unless explicitly opted in.
    /// </summary>
    public bool IncludeInPicker { get; set; }

    /// <summary>
    /// Optional sort order in the operation picker. Lower values appear first.
    /// </summary>
    public int? PickerOrder { get; set; }

    /// <summary>
    /// Gets whether this binding executes a custom prompt action.
    /// </summary>
    public bool IsCustomPromptAction => string.Equals(Action, "custom-prompt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this binding opens a picker for configured presets.
    /// </summary>
    public bool IsPresetPickerAction => string.Equals(Action, "preset-picker", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this binding executes a preset action.
    /// </summary>
    public bool IsPresetAction => string.IsNullOrWhiteSpace(Action)
        || string.Equals(Action, "preset", StringComparison.OrdinalIgnoreCase);
}
