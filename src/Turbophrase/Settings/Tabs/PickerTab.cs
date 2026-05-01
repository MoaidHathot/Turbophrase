using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Edits the operation picker: which entries appear, in which order, and the
/// non-hotkey "picker-only" actions defined under <c>pickerActions</c> in
/// <c>turbophrase.json</c>. Picker order is shared between presets and
/// hotkey/picker actions; lower numbers appear first.
/// </summary>
public sealed class PickerTab : SettingsTabBase
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        EditMode = DataGridViewEditMode.EditOnEnter,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
    };

    private readonly Button _moveUpButton = new() { Text = "Move up", AutoSize = true, Enabled = false };
    private readonly Button _moveDownButton = new() { Text = "Move down", AutoSize = true, Enabled = false };
    private readonly Button _addPickerActionButton = new() { Text = "Add picker-only action...", AutoSize = true };
    private readonly Button _removePickerActionButton = new() { Text = "Remove picker-only action", AutoSize = true, Enabled = false };

    private readonly List<PickerRow> _rows = new();
    private List<string> _availableProviders = new();

    public PickerTab()
    {
        var top = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 6, 8, 0),
            ForeColor = SystemColors.GrayText,
            Text = "Bind a hotkey to action 'preset-picker' to open this list. Use the checkbox to hide entries; use Move up/down to control the order.",
        };
        Controls.Add(top);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Source",
            HeaderText = "Source",
            FillWeight = 18,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Display",
            HeaderText = "Name",
            FillWeight = 35,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Action",
            HeaderText = "Action",
            FillWeight = 20,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Visible",
            HeaderText = "In picker",
            FillWeight = 12,
        });

        _grid.SelectionChanged += (_, _) => OnSelectionChanged();
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _moveUpButton.Click += (_, _) => MoveSelected(-1);
        _moveDownButton.Click += (_, _) => MoveSelected(+1);
        _addPickerActionButton.Click += (_, _) => OnAddPickerActionClicked();
        _removePickerActionButton.Click += (_, _) => OnRemovePickerActionClicked();

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Padding = new Padding(8, 6, 8, 6),
        };
        buttons.Controls.Add(_moveUpButton);
        buttons.Controls.Add(_moveDownButton);
        buttons.Controls.Add(_addPickerActionButton);
        buttons.Controls.Add(_removePickerActionButton);

        Controls.Add(_grid);
        Controls.Add(buttons);
    }

    public override string Title => "Operation picker";

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _availableProviders = config.Providers.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _rows.Clear();

            foreach (var (key, preset) in config.Presets)
            {
                _rows.Add(new PickerRow
                {
                    Source = PickerSource.Preset,
                    Key = key,
                    Display = string.IsNullOrEmpty(preset.Name) ? key : preset.Name,
                    Action = "Preset",
                    IncludeInPicker = preset.IncludeInPicker,
                    PickerOrder = preset.PickerOrder,
                });
            }

            foreach (var hotkey in config.Hotkeys)
            {
                if (hotkey.IsPresetAction)
                {
                    // Preset hotkeys reference a preset that already has its
                    // own row in this list; do not double-list them.
                    continue;
                }

                _rows.Add(new PickerRow
                {
                    Source = PickerSource.Hotkey,
                    Key = SafeId(hotkey),
                    Display = DescribeName(hotkey),
                    Action = ActionLabel(hotkey),
                    IncludeInPicker = hotkey.IncludeInPicker,
                    PickerOrder = hotkey.PickerOrder,
                    OriginalBinding = hotkey,
                });
            }

            foreach (var action in config.PickerActions)
            {
                _rows.Add(new PickerRow
                {
                    Source = PickerSource.PickerAction,
                    Key = SafeId(action),
                    Display = DescribeName(action),
                    Action = ActionLabel(action),
                    IncludeInPicker = action.IncludeInPicker,
                    PickerOrder = action.PickerOrder,
                    OriginalBinding = action,
                });
            }

            // Order by current PickerOrder (nulls last), preserving load order
            // as a tiebreaker.
            int sequence = 0;
            foreach (var row in _rows)
            {
                row.LoadIndex = sequence++;
            }

            _rows.Sort(static (a, b) =>
            {
                var aOrder = a.PickerOrder ?? int.MaxValue;
                var bOrder = b.PickerOrder ?? int.MaxValue;
                var c = aOrder.CompareTo(bOrder);
                return c != 0 ? c : a.LoadIndex.CompareTo(b.LoadIndex);
            });

            RebuildGrid();
        });
    }

    public override string? Validate() => null;

    public override void ApplyTo(ConfigEditor editor)
    {
        // Reassign PickerOrder by current row index so order survives saves.
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].PickerOrder = i + 1;
        }

        // Update presets via SetPreset (preserving prompt etc.). We need the
        // existing PromptPreset values, so re-read them from the file.
        var fileNames = new HashSet<string>(editor.GetPresetNames(), StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows.Where(r => r.Source == PickerSource.Preset))
        {
            if (!fileNames.Contains(row.Key))
            {
                continue;
            }

            var current = ReadPresetFromEditor(editor, row.Key);
            if (current == null)
            {
                continue;
            }

            current.PickerOrder = row.PickerOrder;
            current.IncludeInPicker = row.IncludeInPicker;
            editor.SetPreset(row.Key, current);
        }

        // Update hotkeys (the entries we did not include came back as
        // preset-action rows already merged with presets).
        var hotkeyBindings = new List<HotkeyBinding>();
        foreach (var hotkeyRow in _rows.Where(r => r.Source == PickerSource.Hotkey))
        {
            if (hotkeyRow.OriginalBinding == null)
            {
                continue;
            }

            var b = hotkeyRow.OriginalBinding;
            b.IncludeInPicker = hotkeyRow.IncludeInPicker;
            b.PickerOrder = hotkeyRow.PickerOrder;
            hotkeyBindings.Add(b);
        }

        // Re-add preset-action hotkeys we filtered out at LoadFrom.
        foreach (var row in _rows)
        {
            // We already covered hotkey/picker rows above; don't touch presets.
        }

        // The HotkeysTab is the canonical editor for hotkeys; we only
        // rewrite if the picker changed something. Doing a full replace here
        // would clobber any concurrent edits made on the Hotkeys tab. Since
        // the Settings form runs both LoadFrom and ApplyTo per save, the
        // last writer wins and this is safe -- but to play nicely we merge
        // existing hotkeys with our updated picker fields.
        var existingHotkeys = ReadHotkeysFromEditor(editor);
        for (var i = 0; i < existingHotkeys.Count; i++)
        {
            var existing = existingHotkeys[i];
            if (existing.IsPresetAction)
            {
                continue;
            }

            var match = _rows.FirstOrDefault(r =>
                r.Source == PickerSource.Hotkey &&
                r.OriginalBinding != null &&
                ReferenceEquals(r.OriginalBinding, existing) is false &&
                string.Equals(SafeId(existing), r.Key, StringComparison.Ordinal));
            if (match == null)
            {
                continue;
            }

            existing.IncludeInPicker = match.IncludeInPicker;
            existing.PickerOrder = match.PickerOrder;
            existingHotkeys[i] = existing;
        }
        editor.SetHotkeys(existingHotkeys);

        // Picker actions
        var pickerActions = new List<HotkeyBinding>();
        foreach (var row in _rows.Where(r => r.Source == PickerSource.PickerAction))
        {
            var b = row.OriginalBinding ?? new HotkeyBinding();
            b.IncludeInPicker = row.IncludeInPicker;
            b.PickerOrder = row.PickerOrder;
            pickerActions.Add(b);
        }
        editor.SetPickerActions(pickerActions);
    }

    private void RebuildGrid()
    {
        _grid.Rows.Clear();
        foreach (var row in _rows)
        {
            var grid = new DataGridViewRow();
            grid.CreateCells(_grid, row.SourceLabel, row.Display, row.Action, row.IncludeInPicker);
            _grid.Rows.Add(grid);
        }
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        var hasRow = _grid.SelectedRows.Count > 0;
        _moveUpButton.Enabled = hasRow && _grid.SelectedRows[0].Index > 0;
        _moveDownButton.Enabled = hasRow && _grid.SelectedRows[0].Index < _rows.Count - 1;

        var selected = hasRow && _grid.SelectedRows[0].Index < _rows.Count
            ? _rows[_grid.SelectedRows[0].Index]
            : null;
        _removePickerActionButton.Enabled = selected != null && selected.Source == PickerSource.PickerAction;
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
        {
            return;
        }

        if (_grid.Columns[e.ColumnIndex].Name == "Visible")
        {
            var row = _rows[e.RowIndex];
            var checkValue = _grid.Rows[e.RowIndex].Cells["Visible"].Value;
            row.IncludeInPicker = checkValue is bool b && b;
            MarkDirty();
        }
    }

    private void MoveSelected(int direction)
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }

        var index = _grid.SelectedRows[0].Index;
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _rows.Count)
        {
            return;
        }

        var row = _rows[index];
        _rows.RemoveAt(index);
        _rows.Insert(newIndex, row);
        MarkDirty();
        RebuildGrid();
        if (newIndex < _grid.Rows.Count)
        {
            _grid.ClearSelection();
            _grid.Rows[newIndex].Selected = true;
        }
    }

    private void OnAddPickerActionClicked()
    {
        using var dialog = new AddPickerActionDialog(_availableProviders);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
        {
            return;
        }

        _rows.Add(new PickerRow
        {
            Source = PickerSource.PickerAction,
            Key = SafeId(dialog.Result),
            Display = DescribeName(dialog.Result),
            Action = ActionLabel(dialog.Result),
            IncludeInPicker = true,
            PickerOrder = _rows.Count + 1,
            OriginalBinding = dialog.Result,
        });
        MarkDirty();
        RebuildGrid();
        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[_grid.Rows.Count - 1].Selected = true;
        }
    }

    private void OnRemovePickerActionClicked()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }

        var index = _grid.SelectedRows[0].Index;
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        var row = _rows[index];
        if (row.Source != PickerSource.PickerAction)
        {
            return;
        }

        _rows.RemoveAt(index);
        MarkDirty();
        RebuildGrid();
    }

    private static PromptPreset? ReadPresetFromEditor(ConfigEditor editor, string key)
    {
        // We rely on serialization round-trip; ConfigEditor doesn't expose a
        // "get full preset" API today, so build one from the raw fields it
        // does expose.
        var raw = ReadObject(editor, "presets", key);
        if (raw == null)
        {
            return null;
        }

        return new PromptPreset
        {
            Name = raw.GetValueOrDefault("name") ?? string.Empty,
            SystemPrompt = raw.GetValueOrDefault("systemPrompt") ?? string.Empty,
            Provider = raw.TryGetValue("provider", out var p) && !string.IsNullOrEmpty(p) ? p : null,
            IncludeInPicker = !raw.TryGetValue("includeInPicker", out var inc) || !bool.TryParse(inc, out var v) || v,
            PickerOrder = raw.TryGetValue("pickerOrder", out var ord) && int.TryParse(ord, out var i) ? i : null,
        };
    }

    private static List<HotkeyBinding> ReadHotkeysFromEditor(ConfigEditor editor)
    {
        // ConfigEditor doesn't expose hotkeys directly today. The Settings
        // form reloads the resolved config after save, so the simplest
        // accurate path is to re-load the file. (This path runs at most
        // once per save.)
        var config = ConfigurationService.LoadConfiguration();
        return config.Hotkeys.ToList();
    }

    private static Dictionary<string, string>? ReadObject(ConfigEditor editor, string section, string key)
    {
        // Minimal helper: project ConfigEditor's raw-field accessors into a
        // string dictionary for easy reading. Only used for presets which
        // are flat string/bool/int.
        if (section != "presets")
        {
            return null;
        }

        if (!editor.GetPresetNames().Contains(key, StringComparer.Ordinal))
        {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldName in new[] { "name", "systemPrompt", "provider", "includeInPicker", "pickerOrder" })
        {
            var value = editor.GetPresetRawField(key, fieldName);
            if (value != null)
            {
                dict[fieldName] = value;
            }
        }
        return dict;
    }

    private static string ActionLabel(HotkeyBinding b)
    {
        if (b.IsCustomPromptAction)
        {
            return "Custom prompt";
        }
        if (b.IsPresetPickerAction)
        {
            return "Preset picker";
        }
        if (b.IsPresetAction)
        {
            return "Preset";
        }
        return b.Action ?? "(unknown)";
    }

    private static string DescribeName(HotkeyBinding b)
    {
        if (!string.IsNullOrEmpty(b.Name))
        {
            return b.Name;
        }
        if (b.IsCustomPromptAction)
        {
            return "Custom prompt";
        }
        if (b.IsPresetPickerAction)
        {
            return "Choose Operation";
        }
        return b.Preset;
    }

    private static string SafeId(HotkeyBinding b)
    {
        if (!string.IsNullOrEmpty(b.Name))
        {
            return b.Name;
        }
        if (!string.IsNullOrEmpty(b.Action))
        {
            return $"{b.Action}@{b.Keys}";
        }
        return $"preset:{b.Preset}";
    }

    private enum PickerSource
    {
        Preset,
        Hotkey,
        PickerAction,
    }

    private sealed class PickerRow
    {
        public PickerSource Source { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool IncludeInPicker { get; set; } = true;
        public int? PickerOrder { get; set; }
        public int LoadIndex { get; set; }
        public HotkeyBinding? OriginalBinding { get; set; }

        public string SourceLabel => Source switch
        {
            PickerSource.Preset => "Preset",
            PickerSource.Hotkey => "Hotkey",
            PickerSource.PickerAction => "Picker action",
            _ => "?",
        };
    }

    /// <summary>
    /// Dialog for adding a picker-only action (a custom prompt without a
    /// hotkey binding).
    /// </summary>
    private sealed class AddPickerActionDialog : Form
    {
        private readonly TextBox _nameBox = new() { Width = 280 };
        private readonly ComboBox _providerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private readonly TextBox _templateBox = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 420,
            Height = 140,
        };
        private readonly Label _error = new() { ForeColor = Color.Firebrick, AutoSize = true };
        private const string NoProviderOverride = "(use default provider)";

        public HotkeyBinding? Result { get; private set; }

        public AddPickerActionDialog(IEnumerable<string> providers)
        {
            Text = "Add picker-only action";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 320);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;

            _providerCombo.Items.Add(NoProviderOverride);
            foreach (var p in providers)
            {
                _providerCombo.Items.Add(p);
            }
            _providerCombo.SelectedIndex = 0;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            AddRow(layout, "Display name", _nameBox);
            AddRow(layout, "Provider override", _providerCombo);
            AddRow(layout, "Prompt template", _templateBox);
            AddRow(layout, string.Empty, _error);

            var ok = new Button { Text = "Add", DialogResult = DialogResult.None, Width = 90 };
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
                if (string.IsNullOrWhiteSpace(_nameBox.Text))
                {
                    _error.Text = "Name is required.";
                    return;
                }

                Result = new HotkeyBinding
                {
                    Action = "custom-prompt",
                    Keys = string.Empty,
                    Preset = string.Empty,
                    Name = _nameBox.Text.Trim(),
                    Provider = _providerCombo.SelectedItem as string == NoProviderOverride
                        ? null
                        : _providerCombo.SelectedItem as string,
                    SystemPromptTemplate = string.IsNullOrWhiteSpace(_templateBox.Text) ? null : _templateBox.Text,
                    IncludeInPicker = true,
                };
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static void AddRow(TableLayoutPanel layout, string label, Control control)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 4),
            };
            layout.Controls.Add(lbl, 0, layout.RowCount);
            control.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(control, 1, layout.RowCount);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }
    }
}
