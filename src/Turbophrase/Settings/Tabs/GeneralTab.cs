using Turbophrase.Core.Configuration;
using Turbophrase.Services;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// General settings: default provider, run-at-startup, and the global custom
/// prompt template. This tab does not modify provider definitions themselves;
/// editing presets/providers lands in later iterations.
/// </summary>
public sealed class GeneralTab : SettingsTabBase
{
    private readonly ComboBox _defaultProviderCombo;
    private readonly CheckBox _runAtStartupCheckBox;
    private readonly TextBox _customPromptTextBox;
    private readonly Button _resetCustomPromptButton;
    private readonly Label _runAtStartupHint;

    private string _initialCustomPrompt = string.Empty;
    private bool _initialRunAtStartup;
    private string? _initialDefaultProvider;

    public GeneralTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = false,
            Padding = new Padding(8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        layout.Controls.Add(MakeLabel("Default provider"), 0, 0);
        _defaultProviderCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 280,
            Anchor = AnchorStyles.Left,
        };
        _defaultProviderCombo.SelectedIndexChanged += (_, _) => MarkDirty();
        layout.Controls.Add(_defaultProviderCombo, 1, 0);

        layout.Controls.Add(MakeLabel("Windows startup"), 0, 1);
        _runAtStartupCheckBox = new CheckBox
        {
            Text = "Run Turbophrase at Windows startup",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _runAtStartupCheckBox.CheckedChanged += (_, _) => MarkDirty();
        layout.Controls.Add(_runAtStartupCheckBox, 1, 1);

        _runAtStartupHint = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = string.Empty,
            Anchor = AnchorStyles.Left,
        };
        layout.Controls.Add(new Label { Text = string.Empty }, 0, 2);
        layout.Controls.Add(_runAtStartupHint, 1, 2);

        var promptHeader = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            WrapContents = false,
        };
        promptHeader.Controls.Add(MakeLabel("Custom prompt template"));
        _resetCustomPromptButton = new Button
        {
            Text = "Reset to default",
            AutoSize = true,
            Margin = new Padding(12, 0, 0, 0),
        };
        _resetCustomPromptButton.Click += (_, _) =>
        {
            _customPromptTextBox!.Text = new CustomPromptSettings().SystemPromptTemplate;
        };
        layout.Controls.Add(promptHeader, 0, 3);
        layout.SetColumnSpan(promptHeader, 2);

        _customPromptTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = true,
        };
        _customPromptTextBox.TextChanged += (_, _) => MarkDirty();

        var promptPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            AutoSize = false,
        };
        promptPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        promptPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        promptPanel.Controls.Add(_customPromptTextBox, 0, 0);

        var promptFooter = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = false,
            Padding = new Padding(0, 6, 0, 0),
        };
        promptFooter.Controls.Add(_resetCustomPromptButton);

        var promptHelp = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "Use {instruction} for the user prompt and {text} for the selected text.",
            TextAlign = ContentAlignment.MiddleLeft,
        };
        promptFooter.Controls.Add(promptHelp);

        promptPanel.Controls.Add(promptFooter, 0, 1);

        layout.Controls.Add(promptPanel, 0, 4);
        layout.SetColumnSpan(promptPanel, 2);

        Controls.Add(layout);
    }

    public override string Title => "General";

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _defaultProviderCombo.BeginUpdate();
            _defaultProviderCombo.Items.Clear();

            // Show all configured provider keys, sorted, so the user can pick
            // any of them. We don't filter by "configured" here -- that's the
            // Providers tab's job in a later iteration.
            foreach (var name in config.Providers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                _defaultProviderCombo.Items.Add(name);
            }

            if (!string.IsNullOrEmpty(config.DefaultProvider))
            {
                if (!_defaultProviderCombo.Items.Contains(config.DefaultProvider))
                {
                    _defaultProviderCombo.Items.Add(config.DefaultProvider);
                }

                _defaultProviderCombo.SelectedItem = config.DefaultProvider;
            }
            else if (_defaultProviderCombo.Items.Count > 0)
            {
                _defaultProviderCombo.SelectedIndex = 0;
            }

            _defaultProviderCombo.EndUpdate();
            _initialDefaultProvider = _defaultProviderCombo.SelectedItem as string;

            _runAtStartupCheckBox.Checked = StartupManager.IsEnabled();
            _initialRunAtStartup = _runAtStartupCheckBox.Checked;
            _runAtStartupHint.Text = _runAtStartupCheckBox.Checked
                ? StartupManager.GetStartupCommand() ?? string.Empty
                : "Adds a per-user entry under HKCU\\...\\Run.";

            _customPromptTextBox.Text = config.CustomPrompt.SystemPromptTemplate;
            _initialCustomPrompt = _customPromptTextBox.Text;
        });
    }

    public override string? Validate()
    {
        if (_defaultProviderCombo.SelectedItem is not string provider || string.IsNullOrWhiteSpace(provider))
        {
            return "Default provider must be selected.";
        }

        if (string.IsNullOrWhiteSpace(_customPromptTextBox.Text))
        {
            return "Custom prompt template cannot be empty.";
        }

        return null;
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        if (_defaultProviderCombo.SelectedItem is string provider && provider != _initialDefaultProvider)
        {
            editor.SetDefaultProvider(provider);
        }

        if (_customPromptTextBox.Text != _initialCustomPrompt)
        {
            editor.SetCustomPromptTemplate(_customPromptTextBox.Text);
        }

        ApplyStartupChange();
    }

    private void ApplyStartupChange()
    {
        var current = StartupManager.IsEnabled();
        var desired = _runAtStartupCheckBox.Checked;
        if (current == desired && _initialRunAtStartup == desired)
        {
            return;
        }

        try
        {
            if (desired)
            {
                StartupManager.Enable(ConfigurationService.CustomConfigFilePath);
            }
            else
            {
                StartupManager.Disable();
            }
        }
        catch (Exception ex)
        {
            // Surface but do not abort the rest of the save -- the JSON edits
            // are independent of the registry write.
            MessageBox.Show(
                this,
                $"Failed to update Windows startup setting:\n\n{ex.Message}",
                "Turbophrase Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 6, 0, 0),
    };
}
