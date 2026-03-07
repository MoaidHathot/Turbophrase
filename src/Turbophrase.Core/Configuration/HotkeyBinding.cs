namespace Turbophrase.Core.Configuration;

/// <summary>
/// Binding between a keyboard shortcut and a preset.
/// </summary>
public class HotkeyBinding
{
    /// <summary>
    /// The keyboard shortcut (e.g., "Ctrl+Shift+G").
    /// </summary>
    public string Keys { get; set; } = string.Empty;

    /// <summary>
    /// The preset name to execute when this hotkey is pressed.
    /// </summary>
    public string Preset { get; set; } = string.Empty;
}
