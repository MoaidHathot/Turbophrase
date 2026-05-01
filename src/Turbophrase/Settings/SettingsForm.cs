using Turbophrase.Core.Configuration;
using Turbophrase.Services;
using Turbophrase.Settings.Tabs;

namespace Turbophrase.Settings;

/// <summary>
/// Top-level Settings window. Hosts a <see cref="TabControl"/> with one
/// <see cref="ISettingsTab"/> per page and exposes Save / Apply / Cancel
/// behaviour.
/// </summary>
/// <remarks>
/// The form is non-modal and intended to be shown by
/// <see cref="TrayApplicationContext"/>. Saves are routed through
/// <see cref="ConfigEditor"/> which writes <c>turbophrase.json</c> in place;
/// the existing <see cref="ConfigurationWatcher"/> picks up the change and the
/// tray reloads via its normal hot-reload path.
/// </remarks>
public sealed class SettingsForm : Form
{
    private readonly TabControl _tabs;
    private readonly Button _saveButton;
    private readonly Button _applyButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly List<ISettingsTab> _settingsTabs = new();

    public SettingsForm()
    {
        Text = "Turbophrase Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 520);
        ClientSize = new Size(820, 600);
        ShowInTaskbar = true;
        TryLoadIcon();

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6),
        };

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(12),
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 360,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
        };

        _saveButton = new Button
        {
            Text = "Save",
            Width = 96,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _saveButton.Click += (_, _) => OnSaveClicked(closeOnSuccess: true);

        _applyButton = new Button
        {
            Text = "Apply",
            Width = 96,
            Height = 30,
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _applyButton.Click += (_, _) => OnSaveClicked(closeOnSuccess: false);

        _cancelButton = new Button
        {
            Text = "Close",
            Width = 96,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
        };
        buttonRow.Controls.Add(_cancelButton);
        buttonRow.Controls.Add(_applyButton);
        buttonRow.Controls.Add(_saveButton);

        bottomPanel.Controls.Add(buttonRow);
        bottomPanel.Controls.Add(_statusLabel);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        Controls.Add(_tabs);
        Controls.Add(bottomPanel);

        LoadAndPopulate();
    }

    private void TryLoadIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            var icoPath = Path.Combine(dir, "Turbophrase.ico");
            if (File.Exists(icoPath))
            {
                Icon = new Icon(icoPath);
            }
        }
        catch
        {
            // Icon is non-essential.
        }
    }

    private void LoadAndPopulate()
    {
        TurbophraseConfig config;
        try
        {
            config = ConfigurationService.LoadConfiguration();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Failed to load configuration:\n\n{ex.Message}",
                "Turbophrase Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        AddTab(new GeneralTab(), config);
        AddTab(new ProvidersTab(), config);
        AddTab(new PresetsTab(), config);
        AddTab(new HotkeysTab(), config);
        AddTab(new PickerTab(), config);
        AddTab(new NotificationsTab(), config);
        AddTab(new AdvancedTab(), config);

        UpdateDirtyState();
    }

    private void AddTab(ISettingsTab tab, TurbophraseConfig config)
    {
        var page = new TabPage(tab.Title)
        {
            Padding = new Padding(12),
            UseVisualStyleBackColor = true,
        };

        tab.Control.Dock = DockStyle.Fill;
        page.Controls.Add(tab.Control);
        _tabs.TabPages.Add(page);

        tab.LoadFrom(config);
        tab.DirtyStateChanged += (_, _) => UpdateDirtyState();
        _settingsTabs.Add(tab);
    }

    private void UpdateDirtyState()
    {
        var dirty = _settingsTabs.Any(t => t.IsDirty);
        _applyButton.Enabled = dirty;
        _saveButton.Text = dirty ? "Save" : "Close";
        _statusLabel.Text = dirty ? "Unsaved changes" : string.Empty;
    }

    private void OnSaveClicked(bool closeOnSuccess)
    {
        var anyDirty = _settingsTabs.Any(t => t.IsDirty);
        if (!anyDirty)
        {
            if (closeOnSuccess)
            {
                Close();
            }
            return;
        }

        // Validate every tab first; fail fast and surface the first problem.
        foreach (var tab in _settingsTabs)
        {
            var error = tab.Validate();
            if (error != null)
            {
                FocusTab(tab);
                MessageBox.Show(
                    this,
                    error,
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        try
        {
            var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);
            foreach (var tab in _settingsTabs)
            {
                tab.ApplyTo(editor);
            }
            editor.Save();

            // Reload so subsequent edits see the just-written state and tabs
            // can clear their dirty flags.
            var refreshed = ConfigurationService.LoadConfiguration();
            foreach (var tab in _settingsTabs)
            {
                tab.LoadFrom(refreshed);
            }

            UpdateDirtyState();
            _statusLabel.Text = $"Saved {DateTime.Now:HH:mm:ss}";

            if (closeOnSuccess)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Failed to save configuration:\n\n{ex.Message}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void FocusTab(ISettingsTab tab)
    {
        for (var i = 0; i < _settingsTabs.Count; i++)
        {
            if (ReferenceEquals(_settingsTabs[i], tab))
            {
                _tabs.SelectedIndex = i;
                return;
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_settingsTabs.Any(t => t.IsDirty) && e.CloseReason == CloseReason.UserClosing)
        {
            var result = MessageBox.Show(
                this,
                "You have unsaved changes. Save before closing?",
                "Turbophrase Settings",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            switch (result)
            {
                case DialogResult.Yes:
                    OnSaveClicked(closeOnSuccess: false);
                    if (_settingsTabs.Any(t => t.IsDirty))
                    {
                        // Save failed -- stay open.
                        e.Cancel = true;
                        return;
                    }
                    break;

                case DialogResult.Cancel:
                    e.Cancel = true;
                    return;
            }
        }

        base.OnFormClosing(e);
    }
}
