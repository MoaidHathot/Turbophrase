using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

/// <summary>
/// Dialog for quickly choosing an operation.
/// </summary>
public class PresetPickerDialog : Form
{
    private readonly TextBox _filterTextBox;
    private readonly ListBox _operationListBox;
    private readonly List<PickerOperation> _allOperations;

    public PresetPickerDialog(IEnumerable<PickerOperation> operations)
    {
        Text = "Choose Operation";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(420, 300);
        KeyPreview = true;

        var filterLabel = new Label
        {
            Text = "Operation (type to filter, Enter selects first, Esc cancels)",
            Left = 12,
            Top = 12,
            Width = 396
        };

        _filterTextBox = new TextBox
        {
            Left = 12,
            Top = 34,
            Width = 396
        };

        _operationListBox = new ListBox
        {
            Left = 12,
            Top = 66,
            Width = 396,
            Height = 220,
            IntegralHeight = false
        };

        _allOperations = operations.ToList();

        Controls.AddRange([filterLabel, _filterTextBox, _operationListBox]);

        _filterTextBox.TextChanged += (_, _) => ApplyFilter();
        _filterTextBox.KeyDown += OnFilterTextBoxKeyDown;
        _operationListBox.DoubleClick += (_, _) => SubmitSelectedItem();

        Shown += (_, _) =>
        {
            _filterTextBox.Focus();
            _filterTextBox.SelectAll();
        };

        ApplyFilter();
    }

    public PickerOperation? SelectedOperation { get; private set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        if (keyData == Keys.Enter)
        {
            SubmitSelectedItem();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnFilterTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Down or Keys.Up)
        {
            MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
            e.SuppressKeyPress = true;
            return;
        }

        if (TryGetDigit(e.KeyCode, out var digit) && SelectNumberedItem(digit))
        {
            e.SuppressKeyPress = true;
        }
    }

    private void ApplyFilter()
    {
        var filter = _filterTextBox.Text.Trim();
        var matches = string.IsNullOrWhiteSpace(filter)
            ? _allOperations
            : _allOperations
                .Where(item => item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || item.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _operationListBox.BeginUpdate();
        _operationListBox.Items.Clear();

        for (var index = 0; index < matches.Count; index++)
        {
            _operationListBox.Items.Add(matches[index] with { Number = index + 1 });
        }

        _operationListBox.SelectedIndex = _operationListBox.Items.Count > 0 ? 0 : -1;
        _operationListBox.EndUpdate();
    }

    private void MoveSelection(int delta)
    {
        if (_operationListBox.Items.Count == 0)
        {
            return;
        }

        var currentIndex = _operationListBox.SelectedIndex >= 0 ? _operationListBox.SelectedIndex : 0;
        var nextIndex = Math.Clamp(currentIndex + delta, 0, _operationListBox.Items.Count - 1);
        _operationListBox.SelectedIndex = nextIndex;
    }

    private bool SelectNumberedItem(int digit)
    {
        var index = digit == 0 ? 9 : digit - 1;
        if (index < 0 || index >= _operationListBox.Items.Count)
        {
            return false;
        }

        _operationListBox.SelectedIndex = index;
        return true;
    }

    private void SubmitSelectedItem()
    {
        if (_operationListBox.SelectedItem is not PickerOperation selectedItem)
        {
            return;
        }

        SelectedOperation = selectedItem;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryGetDigit(Keys keyCode, out int digit)
    {
        if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
        {
            digit = keyCode - Keys.D0;
            return true;
        }

        if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
        {
            digit = keyCode - Keys.NumPad0;
            return true;
        }

        digit = 0;
        return false;
    }
}

public sealed record PickerOperation(string Id, string DisplayName, HotkeyBinding Binding)
{
    public int Number { get; init; }

    public override string ToString() => $"{Number}. {DisplayName}";
}
