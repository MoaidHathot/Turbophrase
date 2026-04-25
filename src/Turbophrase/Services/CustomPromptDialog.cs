namespace Turbophrase.Services;

/// <summary>
/// Dialog for entering a one-off prompt before transforming selected text.
/// </summary>
public class CustomPromptDialog : Form
{
    private readonly TextBox _promptTextBox;
    private readonly ComboBox _providerComboBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public CustomPromptDialog(IEnumerable<string> providers, string defaultProvider)
    {
        Text = "Custom Prompt";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(520, 280);

        var promptLabel = new Label
        {
            Text = "Prompt (Ctrl+Enter to submit, Esc to cancel)",
            Left = 12,
            Top = 12,
            Width = 360
        };

        _promptTextBox = new TextBox
        {
            Left = 12,
            Top = 34,
            Width = 496,
            Height = 150,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = true
        };

        var providerLabel = new Label
        {
            Text = "Provider",
            Left = 12,
            Top = 196,
            Width = 100
        };

        _providerComboBox = new ComboBox
        {
            Left = 12,
            Top = 218,
            Width = 240,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        foreach (var provider in providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _providerComboBox.Items.Add(provider);
        }

        if (_providerComboBox.Items.Count > 0)
        {
            var defaultIndex = _providerComboBox.FindStringExact(defaultProvider);
            _providerComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
        }

        _okButton = new Button
        {
            Text = "OK (Ctrl+Enter)",
            DialogResult = DialogResult.OK,
            Left = 352,
            Top = 236,
            Width = 75
        };

        _cancelButton = new Button
        {
            Text = "Cancel (Esc)",
            DialogResult = DialogResult.Cancel,
            Left = 433,
            Top = 236,
            Width = 75
        };

        Controls.AddRange([promptLabel, _promptTextBox, providerLabel, _providerComboBox, _okButton, _cancelButton]);
        CancelButton = _cancelButton;
        KeyPreview = true;

        _promptTextBox.KeyDown += OnPromptTextBoxKeyDown;
    }

    public string PromptText => _promptTextBox.Text.Trim();

    public string? SelectedProvider => _providerComboBox.SelectedItem as string;

    private void OnPromptTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            Submit();
            e.SuppressKeyPress = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter))
        {
            Submit();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Submit()
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}
