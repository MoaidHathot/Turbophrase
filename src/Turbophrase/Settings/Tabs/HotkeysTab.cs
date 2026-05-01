using Turbophrase.Core.Configuration;
using Turbophrase.Settings.Controls;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Editor for the <c>hotkeys</c> array. Each row binds a key combination to
/// either a preset, a one-off custom prompt action, or the preset picker
/// dialog. The hotkey string format matches what
/// <c>GlobalHotkeyService</c> consumes today.
/// </summary>
public sealed class HotkeysTab : SettingsTabBase
{
    private const string ActionPreset = "preset";
    private const string ActionCustomPrompt = "custom-prompt";
    private const string ActionPresetPicker = "preset-picker";

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        EditMode = DataGridViewEditMode.EditProgrammatically,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        ReadOnly = true,
    };

    private readonly Button _addButton = new() { Text = "Add hotkey...", AutoSize = true };
    private readonly Button _editButton = new() { Text = "Edit...", AutoSize = true, Enabled = false };
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true, Enabled = false };
    private readonly Label _conflictLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Bottom,
        Height = 28,
        ForeColor = Color.Firebrick,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 8, 0),
    };

    private readonly List<HotkeyBinding> _bindings = new();
    private List<string> _availablePresets = new();
    private List<string> _availableProviders = new();

    public HotkeysTab()
    {
        var top = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 32,
            Text = "Hotkeys are global. They work even when Turbophrase is not the active window. Function keys may be used without modifiers; everything else needs Ctrl, Alt, Shift, or Win.",
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(8, 6, 8, 0),
        };
        Controls.Add(top);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Keys",
            HeaderText = "Hotkey",
            FillWeight = 25,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Action",
            HeaderText = "Action",
            FillWeight = 18,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Target",
            HeaderText = "Target / Name",
            FillWeight = 35,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Picker",
            HeaderText = "In picker",
            FillWeight = 12,
            ReadOnly = true,
        });

        _grid.SelectionChanged += (_, _) => OnSelectionChanged();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OnEditClicked();
            }
        };
        _addButton.Click += (_, _) => OnAddClicked();
        _editButton.Click += (_, _) => OnEditClicked();
        _removeButton.Click += (_, _) => OnRemoveClicked();

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Padding = new Padding(8, 6, 8, 6),
        };
        buttons.Controls.Add(_addButton);
        buttons.Controls.Add(_editButton);
        buttons.Controls.Add(_removeButton);

        Controls.Add(_grid);
        Controls.Add(_conflictLabel);
        Controls.Add(buttons);
    }

    public override string Title => "Hotkeys";

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _availablePresets = config.Presets.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _availableProviders = config.Providers.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _bindings.Clear();
            foreach (var binding in config.Hotkeys)
            {
                _bindings.Add(Clone(binding));
            }

            RebuildGrid();
        });
    }

    public override string? Validate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in _bindings)
        {
            if (string.IsNullOrWhiteSpace(b.Keys))
            {
                return "Every hotkey row needs a key combination.";
            }

            if (!seen.Add(b.Keys))
            {
                return $"Hotkey '{b.Keys}' is bound more than once.";
            }

            if (b.IsPresetAction && string.IsNullOrWhiteSpace(b.Preset))
            {
                return $"Hotkey '{b.Keys}' must point at a preset.";
            }
        }

        return null;
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        editor.SetHotkeys(_bindings);
    }

    private void RebuildGrid()
    {
        var previousIndex = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Index : -1;

        _grid.Rows.Clear();
        foreach (var b in _bindings)
        {
            var (action, target) = DescribeBinding(b);
            var row = new DataGridViewRow();
            row.CreateCells(_grid, b.Keys, action, target, b.IncludeInPicker);
            _grid.Rows.Add(row);
        }

        if (previousIndex >= 0 && previousIndex < _grid.Rows.Count)
        {
            _grid.Rows[previousIndex].Selected = true;
        }

        UpdateConflictLabel();
        OnSelectionChanged();
    }

    private void UpdateConflictLabel()
    {
        var duplicates = _bindings
            .GroupBy(b => b.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
            .Select(g => g.Key!)
            .ToList();

        _conflictLabel.Text = duplicates.Count == 0
            ? string.Empty
            : $"Conflict: {string.Join(", ", duplicates)} is bound more than once.";
    }

    private void OnSelectionChanged()
    {
        var hasRow = _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Index < _bindings.Count;
        _editButton.Enabled = hasRow;
        _removeButton.Enabled = hasRow;
    }

    private void OnAddClicked()
    {
        var binding = new HotkeyBinding
        {
            Action = ActionPreset,
            Preset = _availablePresets.FirstOrDefault() ?? string.Empty,
        };

        using var dialog = new HotkeyEditorDialog(binding, _availablePresets, _availableProviders);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _bindings.Add(dialog.Result);
        MarkDirty();
        RebuildGrid();
        SelectRow(_bindings.Count - 1);
    }

    private void OnEditClicked()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }

        var index = _grid.SelectedRows[0].Index;
        if (index < 0 || index >= _bindings.Count)
        {
            return;
        }

        using var dialog = new HotkeyEditorDialog(Clone(_bindings[index]), _availablePresets, _availableProviders);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _bindings[index] = dialog.Result;
        MarkDirty();
        RebuildGrid();
        SelectRow(index);
    }

    private void OnRemoveClicked()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }

        var index = _grid.SelectedRows[0].Index;
        if (index < 0 || index >= _bindings.Count)
        {
            return;
        }

        _bindings.RemoveAt(index);
        MarkDirty();
        RebuildGrid();
        SelectRow(Math.Min(index, _bindings.Count - 1));
    }

    private void SelectRow(int index)
    {
        if (index < 0 || index >= _grid.Rows.Count)
        {
            return;
        }

        _grid.ClearSelection();
        _grid.Rows[index].Selected = true;
    }

    private static (string Action, string Target) DescribeBinding(HotkeyBinding b)
    {
        if (b.IsCustomPromptAction)
        {
            var name = string.IsNullOrEmpty(b.Name) ? "Custom prompt" : b.Name;
            return ("Custom prompt", name + (string.IsNullOrEmpty(b.Provider) ? "" : $"  ({b.Provider})"));
        }

        if (b.IsPresetPickerAction)
        {
            var name = string.IsNullOrEmpty(b.Name) ? "Choose Operation" : b.Name;
            return ("Preset picker", name);
        }

        return ("Preset", b.Preset);
    }

    private static HotkeyBinding Clone(HotkeyBinding b) => new()
    {
        Keys = b.Keys,
        Action = b.Action,
        Preset = b.Preset,
        Name = b.Name,
        SystemPromptTemplate = b.SystemPromptTemplate,
        Provider = b.Provider,
        IncludeInPicker = b.IncludeInPicker,
        PickerOrder = b.PickerOrder,
    };

    /// <summary>
    /// Modal editor for a single <see cref="HotkeyBinding"/>.
    /// </summary>
    private sealed class HotkeyEditorDialog : Form
    {
        private readonly HotkeyCaptureBox _captureBox = new() { Width = 280 };
        private readonly ComboBox _actionCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly ComboBox _presetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox _nameBox = new() { Width = 280 };
        private readonly ComboBox _providerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox _templateBox = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Width = 420,
            Height = 100,
        };
        private readonly CheckBox _includeInPicker = new()
        {
            Text = "Include this action in the operation picker",
            AutoSize = true,
        };
        private readonly NumericUpDown _pickerOrder = new() { Minimum = 0, Maximum = 9999, Width = 80 };
        private readonly Label _error = new() { ForeColor = Color.Firebrick, AutoSize = true };

        private readonly Label _presetLabel = new() { Text = "Preset", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) };
        private readonly Label _nameLabel = new() { Text = "Display name", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) };
        private readonly Label _providerLabel = new() { Text = "Provider override", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) };
        private readonly Label _templateLabel = new() { Text = "Prompt template", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) };

        private const string NoProviderOverride = "(use default provider)";

        public HotkeyBinding Result { get; private set; }

        public HotkeyEditorDialog(HotkeyBinding binding, IEnumerable<string> presets, IEnumerable<string> providers)
        {
            Result = binding;
            Text = "Hotkey";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 460);
            ShowInTaskbar = false;

            _actionCombo.Items.AddRange(new object[]
            {
                "Run preset",
                "Open custom prompt dialog",
                "Open preset picker",
            });

            foreach (var p in presets)
            {
                _presetCombo.Items.Add(p);
            }
            if (_presetCombo.Items.Count == 0)
            {
                _presetCombo.Items.Add("(no presets defined)");
                _presetCombo.Enabled = false;
            }

            _providerCombo.Items.Add(NoProviderOverride);
            foreach (var p in providers)
            {
                _providerCombo.Items.Add(p);
            }

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoScroll = true,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            AddRow(layout, "Keys (press combo)", _captureBox);

            var hint = new Label
            {
                Text = "Press Backspace to clear.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 8),
            };
            AddRow(layout, string.Empty, hint);

            AddRow(layout, "Action", _actionCombo);
            AddRow(layout, _presetLabel, _presetCombo);
            AddRow(layout, _nameLabel, _nameBox);
            AddRow(layout, _providerLabel, _providerCombo);
            AddRow(layout, _templateLabel, _templateBox);
            AddRow(layout, string.Empty, _includeInPicker);
            AddRow(layout, "Picker order", _pickerOrder);
            AddRow(layout, string.Empty, _error);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.None, Width = 90 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(12),
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            ok.Click += (_, _) =>
            {
                if (TryBuildResult(out var result, out var error))
                {
                    Result = result;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _error.Text = error;
                }
            };

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            _actionCombo.SelectedIndexChanged += (_, _) => UpdateVisibility();

            // Populate
            _captureBox.CapturedHotkey = binding.Keys ?? string.Empty;

            if (binding.IsCustomPromptAction)
            {
                _actionCombo.SelectedIndex = 1;
            }
            else if (binding.IsPresetPickerAction)
            {
                _actionCombo.SelectedIndex = 2;
            }
            else
            {
                _actionCombo.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(binding.Preset) && _presetCombo.Items.Contains(binding.Preset))
            {
                _presetCombo.SelectedItem = binding.Preset;
            }
            else if (_presetCombo.Items.Count > 0 && _presetCombo.Enabled)
            {
                _presetCombo.SelectedIndex = 0;
            }

            _nameBox.Text = binding.Name ?? string.Empty;
            _templateBox.Text = binding.SystemPromptTemplate ?? string.Empty;

            if (string.IsNullOrEmpty(binding.Provider))
            {
                _providerCombo.SelectedItem = NoProviderOverride;
            }
            else if (_providerCombo.Items.Contains(binding.Provider))
            {
                _providerCombo.SelectedItem = binding.Provider;
            }
            else
            {
                _providerCombo.Items.Add(binding.Provider);
                _providerCombo.SelectedItem = binding.Provider;
            }

            _includeInPicker.Checked = binding.IncludeInPicker;
            _pickerOrder.Value = Math.Max(0, Math.Min(9999, binding.PickerOrder ?? 0));

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var index = _actionCombo.SelectedIndex;
            var isPreset = index == 0;
            var isCustomPrompt = index == 1;
            var isPicker = index == 2;

            _presetLabel.Visible = _presetCombo.Visible = isPreset;
            _nameLabel.Visible = _nameBox.Visible = isCustomPrompt || isPicker;
            _providerLabel.Visible = _providerCombo.Visible = isCustomPrompt;
            _templateLabel.Visible = _templateBox.Visible = isCustomPrompt;
            _includeInPicker.Visible = isCustomPrompt; // Preset has its own picker fields; picker action itself is the picker.
            _pickerOrder.Visible = isCustomPrompt;
        }

        private bool TryBuildResult(out HotkeyBinding result, out string error)
        {
            result = new HotkeyBinding();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(_captureBox.CapturedHotkey))
            {
                error = "Press the desired key combination first.";
                return false;
            }

            result.Keys = _captureBox.CapturedHotkey;

            switch (_actionCombo.SelectedIndex)
            {
                case 0:
                    if (_presetCombo.SelectedItem is not string preset || !_presetCombo.Enabled)
                    {
                        error = "Select a preset to bind to.";
                        return false;
                    }
                    result.Action = ActionPreset;
                    result.Preset = preset;
                    break;

                case 1:
                    result.Action = ActionCustomPrompt;
                    result.Preset = string.Empty;
                    result.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text.Trim();
                    result.SystemPromptTemplate = string.IsNullOrWhiteSpace(_templateBox.Text)
                        ? null
                        : _templateBox.Text;
                    if (_providerCombo.SelectedItem is string p && p != NoProviderOverride)
                    {
                        result.Provider = p;
                    }
                    result.IncludeInPicker = _includeInPicker.Checked;
                    result.PickerOrder = (int)_pickerOrder.Value == 0 ? null : (int)_pickerOrder.Value;
                    break;

                case 2:
                    result.Action = ActionPresetPicker;
                    result.Preset = string.Empty;
                    result.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text.Trim();
                    break;

                default:
                    error = "Pick an action.";
                    return false;
            }

            return true;
        }

        private static void AddRow(TableLayoutPanel layout, object label, Control control)
        {
            Control labelControl = label switch
            {
                Label existing => existing,
                string text => new Label
                {
                    Text = text,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 8, 8, 4),
                },
                _ => new Label { Text = string.Empty, AutoSize = true },
            };
            layout.Controls.Add(labelControl, 0, layout.RowCount);
            control.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(control, 1, layout.RowCount);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }
    }
}
