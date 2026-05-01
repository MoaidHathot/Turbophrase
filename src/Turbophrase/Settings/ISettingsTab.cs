using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings;

/// <summary>
/// Common contract for tabs hosted inside <see cref="SettingsForm"/>.
/// Tabs read state from a <see cref="TurbophraseConfig"/> snapshot and write
/// changes back through a <see cref="ConfigEditor"/> on save.
/// </summary>
public interface ISettingsTab
{
    /// <summary>
    /// Display title shown on the parent <see cref="TabControl"/>.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Underlying <see cref="Control"/> rendered inside the tab page.
    /// </summary>
    Control Control { get; }

    /// <summary>
    /// Whether the user has changed any field in this tab.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Raised whenever <see cref="IsDirty"/> transitions to <c>true</c>.
    /// </summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>
    /// Loads UI state from the given configuration snapshot.
    /// Implementations should clear their dirty flag after loading.
    /// </summary>
    void LoadFrom(TurbophraseConfig config);

    /// <summary>
    /// Validates the current UI state. Returns null on success or an error
    /// message that should be shown to the user.
    /// </summary>
    string? Validate();

    /// <summary>
    /// Applies UI state to the given <see cref="ConfigEditor"/>. Called inside
    /// the parent form's save flow after successful validation.
    /// </summary>
    void ApplyTo(ConfigEditor editor);
}
