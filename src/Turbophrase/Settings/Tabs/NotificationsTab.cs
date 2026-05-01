using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Toggles for every <see cref="NotificationSettings"/> field.
/// </summary>
public sealed class NotificationsTab : SettingsTabBase
{
    private readonly CheckBox _showOnStartup = MakeCheck("Show notification on startup");
    private readonly CheckBox _showOnSuccess = MakeCheck("Show notification on successful transformation");
    private readonly CheckBox _showOnError = MakeCheck("Show notification on errors");
    private readonly CheckBox _showOnConfigReload = MakeCheck("Show notification on configuration reload");
    private readonly CheckBox _showOnProviderChange = MakeCheck("Show notification on provider change");
    private readonly CheckBox _showProcessingOverlay = MakeCheck("Show processing overlay while transforming");
    private readonly CheckBox _showProcessingAnimation = MakeCheck("Animate tray icon while processing");

    public NotificationsTab()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = false,
            WrapContents = false,
            Padding = new Padding(8),
        };

        foreach (var box in new[]
        {
            _showOnStartup,
            _showOnSuccess,
            _showOnError,
            _showOnConfigReload,
            _showOnProviderChange,
            _showProcessingOverlay,
            _showProcessingAnimation,
        })
        {
            box.CheckedChanged += (_, _) => MarkDirty();
            box.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(box);
        }

        Controls.Add(layout);
    }

    public override string Title => "Notifications";

    public override void LoadFrom(TurbophraseConfig config)
    {
        var n = config.Notifications;
        WithoutDirty(() =>
        {
            _showOnStartup.Checked = n.ShowOnStartup;
            _showOnSuccess.Checked = n.ShowOnSuccess;
            _showOnError.Checked = n.ShowOnError;
            _showOnConfigReload.Checked = n.ShowOnConfigReload;
            _showOnProviderChange.Checked = n.ShowOnProviderChange;
            _showProcessingOverlay.Checked = n.ShowProcessingOverlay;
            _showProcessingAnimation.Checked = n.ShowProcessingAnimation;
        });
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        var settings = new NotificationSettings
        {
            ShowOnStartup = _showOnStartup.Checked,
            ShowOnSuccess = _showOnSuccess.Checked,
            ShowOnError = _showOnError.Checked,
            ShowOnConfigReload = _showOnConfigReload.Checked,
            ShowOnProviderChange = _showOnProviderChange.Checked,
            ShowProcessingOverlay = _showProcessingOverlay.Checked,
            ShowProcessingAnimation = _showProcessingAnimation.Checked,
        };
        editor.SetNotifications(settings);
    }

    private static CheckBox MakeCheck(string text) => new()
    {
        Text = text,
        AutoSize = true,
    };
}
