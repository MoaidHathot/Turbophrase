using System.Diagnostics;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Read-only summary of the resolved configuration paths plus diagnostic
/// logging toggles and quick-access buttons for power users.
/// </summary>
public sealed class AdvancedTab : SettingsTabBase
{
    private readonly TextBox _configPathBox;
    private readonly TextBox _configDirBox;
    private readonly TextBox _customConfigBox;
    private readonly Label _xdgValueLabel;
    private readonly CheckBox _enableLoggingCheckBox;
    private readonly Label _logPathLabel;
    private readonly Button _openConfigFolderButton;
    private readonly Button _openConfigEditorButton;
    private readonly Button _openLogButton;
    private readonly Button _resetToDefaultsButton;

    public AdvancedTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = false,
            Padding = new Padding(8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddRow(layout, "Config file", _configPathBox = MakeReadOnlyBox());
        AddRow(layout, "Config folder", _configDirBox = MakeReadOnlyBox());
        AddRow(layout, "Custom (--config)", _customConfigBox = MakeReadOnlyBox());
        AddRow(layout, "XDG_CONFIG_HOME", _xdgValueLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 6),
        });

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 6),
        };

        _openConfigFolderButton = new Button { Text = "Open config folder", AutoSize = true };
        _openConfigFolderButton.Click += (_, _) => SafeStart(ConfigurationService.ConfigDirectory, isFolder: true);

        _openConfigEditorButton = new Button { Text = "Open in default editor", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _openConfigEditorButton.Click += (_, _) => SafeStart(ConfigurationService.ConfigFilePath, isFolder: false);

        _resetToDefaultsButton = new Button { Text = "Reset to defaults...", AutoSize = true, Margin = new Padding(24, 0, 0, 0) };
        _resetToDefaultsButton.Click += (_, _) => OnResetToDefaultsClicked();

        buttonRow.Controls.Add(_openConfigFolderButton);
        buttonRow.Controls.Add(_openConfigEditorButton);
        buttonRow.Controls.Add(_resetToDefaultsButton);

        var emptyLabel = new Label { Text = string.Empty, AutoSize = true };
        layout.Controls.Add(emptyLabel, 0, layout.RowCount);
        layout.Controls.Add(buttonRow, 1, layout.RowCount);
        layout.RowCount++;

        var separator = new Label
        {
            Text = "Diagnostics",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 16, 0, 6),
        };
        layout.Controls.Add(separator, 0, layout.RowCount);
        layout.SetColumnSpan(separator, 2);
        layout.RowCount++;

        _enableLoggingCheckBox = new CheckBox
        {
            Text = "Write diagnostic events to turbophrase.log",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4),
        };
        _enableLoggingCheckBox.CheckedChanged += (_, _) => MarkDirty();
        layout.Controls.Add(new Label { Text = "Logging", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) }, 0, layout.RowCount);
        layout.Controls.Add(_enableLoggingCheckBox, 1, layout.RowCount);
        layout.RowCount++;

        _logPathLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 6),
        };
        _openLogButton = new Button
        {
            Text = "Open log",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0),
        };
        _openLogButton.Click += (_, _) =>
        {
            var path = GetLogPath();
            if (File.Exists(path))
            {
                SafeStart(path, isFolder: false);
            }
            else
            {
                MessageBox.Show(this, $"No log file at:\n{path}", "Logging", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var logFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 0),
        };
        logFlow.Controls.Add(_logPathLabel);
        logFlow.Controls.Add(_openLogButton);
        layout.Controls.Add(new Label { Text = string.Empty, AutoSize = true }, 0, layout.RowCount);
        layout.Controls.Add(logFlow, 1, layout.RowCount);
        layout.RowCount++;

        Controls.Add(layout);
    }

    public override string Title => "Advanced";

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _configPathBox.Text = ConfigurationService.ConfigFilePath;
            _configDirBox.Text = ConfigurationService.ConfigDirectory;
            _customConfigBox.Text = ConfigurationService.CustomConfigFilePath ?? "(not set, change with --config)";

            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            _xdgValueLabel.Text = string.IsNullOrEmpty(xdg)
                ? "(not set)"
                : xdg;

            _enableLoggingCheckBox.Checked = config.Logging.Enabled;
            _logPathLabel.Text = $"Log path: {GetLogPath()}";
        });
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        editor.SetLogging(new LoggingSettings
        {
            Enabled = _enableLoggingCheckBox.Checked,
        });
    }

    private void OnResetToDefaultsClicked()
    {
        var result = MessageBox.Show(
            this,
            "Reset turbophrase.json to default content?\n\nThe current file will be backed up next to it (.bak-<timestamp>).",
            "Reset configuration",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (result != DialogResult.OK)
        {
            return;
        }

        try
        {
            var backup = ConfigEditor.ResetToDefaults(ConfigurationService.ConfigFilePath, createBackup: true);
            var msg = backup != null
                ? $"Reset complete. Backup written to:\n{backup}"
                : "Reset complete.";
            MessageBox.Show(this, msg, "Reset configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Reset failed:\n\n{ex.Message}", "Reset configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetLogPath() => Path.Combine(ConfigurationService.ConfigDirectory, "turbophrase.log");

    private static void SafeStart(string path, bool isFolder)
    {
        try
        {
            if (isFolder)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                Process.Start("explorer.exe", $"\"{path}\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open '{path}':\n\n{ex.Message}", "Turbophrase", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static TextBox MakeReadOnlyBox() => new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = SystemColors.Control,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Width = 600,
        Margin = new Padding(0, 6, 0, 6),
    };

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var lbl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 0),
        };
        layout.Controls.Add(lbl, 0, layout.RowCount);
        layout.Controls.Add(control, 1, layout.RowCount);
        layout.RowCount++;
    }
}
