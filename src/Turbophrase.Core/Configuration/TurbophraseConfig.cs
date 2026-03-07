namespace Turbophrase.Core.Configuration;

/// <summary>
/// Root configuration for Turbophrase application.
/// </summary>
public class TurbophraseConfig
{
    /// <summary>
    /// The default provider name to use when a preset doesn't specify one.
    /// </summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>
    /// Dictionary of configured AI providers.
    /// </summary>
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    /// <summary>
    /// List of hotkey bindings.
    /// </summary>
    public List<HotkeyBinding> Hotkeys { get; set; } = new();

    /// <summary>
    /// Dictionary of prompt presets.
    /// </summary>
    public Dictionary<string, PromptPreset> Presets { get; set; } = new();

    /// <summary>
    /// Notification settings for controlling which notifications to show.
    /// </summary>
    public NotificationSettings Notifications { get; set; } = new();
}

/// <summary>
/// Settings for controlling notification behavior.
/// </summary>
public class NotificationSettings
{
    /// <summary>
    /// Show notification on application startup.
    /// </summary>
    public bool ShowOnStartup { get; set; } = true;

    /// <summary>
    /// Show notification on successful transformation.
    /// </summary>
    public bool ShowOnSuccess { get; set; } = true;

    /// <summary>
    /// Show notification on transformation error.
    /// </summary>
    public bool ShowOnError { get; set; } = true;

    /// <summary>
    /// Show notification on configuration reload.
    /// </summary>
    public bool ShowOnConfigReload { get; set; } = true;

    /// <summary>
    /// Show notification on provider change.
    /// </summary>
    public bool ShowOnProviderChange { get; set; } = true;

    /// <summary>
    /// Show the processing overlay while transforming text.
    /// </summary>
    public bool ShowProcessingOverlay { get; set; } = true;

    /// <summary>
    /// Animate the tray icon while processing.
    /// </summary>
    public bool ShowProcessingAnimation { get; set; } = true;
}
