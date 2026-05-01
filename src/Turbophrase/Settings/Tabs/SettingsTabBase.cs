using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Convenience base class implementing <see cref="ISettingsTab"/> dirty
/// tracking. Concrete tabs derive from this and only need to wire up their
/// child controls and override <c>LoadFrom</c>/<c>ApplyTo</c>.
/// </summary>
public abstract class SettingsTabBase : UserControl, ISettingsTab
{
    private bool _isDirty;
    private bool _suppressDirty;

    public abstract string Title { get; }

    public Control Control => this;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? DirtyStateChanged;

    public abstract void LoadFrom(TurbophraseConfig config);

    public new virtual string? Validate() => null;

    public abstract void ApplyTo(ConfigEditor editor);

    /// <summary>
    /// Marks the tab as modified. No-op while <see cref="LoadFrom"/> is running.
    /// </summary>
    protected void MarkDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        IsDirty = true;
    }

    /// <summary>
    /// Resets the dirty flag. Used after Apply succeeds and after LoadFrom.
    /// </summary>
    protected void ClearDirty() => IsDirty = false;

    /// <summary>
    /// Suppresses dirty notifications while the supplied action runs. Use
    /// inside <see cref="LoadFrom"/> when populating controls programmatically.
    /// </summary>
    protected void WithoutDirty(Action action)
    {
        var prior = _suppressDirty;
        _suppressDirty = true;
        try
        {
            action();
        }
        finally
        {
            _suppressDirty = prior;
        }

        ClearDirty();
    }
}
