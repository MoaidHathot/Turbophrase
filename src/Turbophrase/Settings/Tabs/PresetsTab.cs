using Turbophrase.Core.Configuration;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Master/detail editor for the <c>presets</c> section. Presets are keyed by
/// a short identifier (used by hotkeys and the picker) and have a name,
/// system prompt, optional provider override, and picker visibility.
/// </summary>
public sealed class PresetsTab : SettingsTabBase
{
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
    };

    private readonly Button _addButton = new() { Text = "Add...", AutoSize = true };
    private readonly Button _duplicateButton = new() { Text = "Duplicate", AutoSize = true, Enabled = false };
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true, Enabled = false };

    private readonly Label _detailHeader = new()
    {
        AutoSize = true,
        Font = new Font(SystemFonts.DefaultFont!.FontFamily, 11f, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly TextBox _keyBox = new() { Width = 220 };
    private readonly TextBox _nameBox = new() { Width = 320 };
    private readonly TextBox _systemPromptBox = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        AcceptsReturn = true,
        AcceptsTab = true,
        Font = new Font(FontFamily.GenericMonospace, 9f),
        WordWrap = true,
        Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
    };
    private readonly ComboBox _providerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _reasoningEffortCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown _pickerOrderNumeric = new() { Minimum = 0, Maximum = 9999, Width = 80 };
    private readonly CheckBox _includeInPickerCheckBox = new() { Text = "Include in operation picker", AutoSize = true };

    private readonly Dictionary<string, PresetEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedKey;
    private bool _suppressEvents;
    private const string NoProviderOverride = "(use default provider)";
    private const string ReasoningInheritLabel = "(use provider default)";

    /// <summary>
    /// Ordered list of choices shown in the reasoning-effort dropdown.
    /// First entry maps to <c>null</c> (Inherit / use provider default);
    /// the rest map 1:1 to the <see cref="ReasoningEffort"/> enum.
    /// </summary>
    private static readonly (string Label, ReasoningEffort? Value)[] ReasoningChoices =
    {
        (ReasoningInheritLabel, null),
        ("Off", ReasoningEffort.Off),
        ("Minimal", ReasoningEffort.Minimal),
        ("Low", ReasoningEffort.Low),
        ("Medium", ReasoningEffort.Medium),
        ("High", ReasoningEffort.High),
        ("XHigh", ReasoningEffort.XHigh),
    };

    public PresetsTab()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 220,
            FixedPanel = FixedPanel.Panel1,
        };

        // Left
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftLayout.Controls.Add(_list, 0, 0);

        var leftButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 0),
        };
        leftButtons.Controls.Add(_addButton);
        leftButtons.Controls.Add(_duplicateButton);
        leftButtons.Controls.Add(_removeButton);
        leftLayout.Controls.Add(leftButtons, 0, 1);

        split.Panel1.Padding = new Padding(0, 0, 8, 0);
        split.Panel1.Controls.Add(leftLayout);

        // Right
        split.Panel2.Padding = new Padding(8, 0, 0, 0);
        split.Panel2.Controls.Add(BuildDetailPanel());

        Controls.Add(split);

        _list.SelectedIndexChanged += (_, _) => OnSelectionChanged();
        _addButton.Click += (_, _) => OnAddClicked();
        _duplicateButton.Click += (_, _) => OnDuplicateClicked();
        _removeButton.Click += (_, _) => OnRemoveClicked();

        _keyBox.TextChanged += (_, _) => OnKeyEdited();
        _nameBox.TextChanged += (_, _) => OnFieldEdited(e => e.Name = _nameBox.Text);
        _systemPromptBox.TextChanged += (_, _) => OnFieldEdited(e => e.SystemPrompt = _systemPromptBox.Text);
        _providerCombo.SelectedIndexChanged += (_, _) => OnFieldEdited(e =>
        {
            e.Provider = _providerCombo.SelectedItem as string == NoProviderOverride
                ? null
                : _providerCombo.SelectedItem as string;
        });
        _reasoningEffortCombo.SelectedIndexChanged += (_, _) => OnFieldEdited(e =>
        {
            var idx = _reasoningEffortCombo.SelectedIndex;
            e.ReasoningEffort = idx >= 0 && idx < ReasoningChoices.Length
                ? ReasoningChoices[idx].Value
                : null;
        });
        _pickerOrderNumeric.ValueChanged += (_, _) => OnFieldEdited(e =>
            e.PickerOrder = (int)_pickerOrderNumeric.Value);
        _includeInPickerCheckBox.CheckedChanged += (_, _) => OnFieldEdited(e =>
            e.IncludeInPicker = _includeInPickerCheckBox.Checked);
    }

    public override string Title => "Presets";

    private Control BuildDetailPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 0,
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        layout.Controls.Add(_detailHeader, 0, 0);
        layout.SetColumnSpan(_detailHeader, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        AddRow(layout, "Key", _keyBox);
        AddRow(layout, "Display name", _nameBox);

        // System prompt: gets all remaining vertical space.
        var promptLabel = new Label
        {
            Text = "System prompt",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 8, 8, 4),
        };
        layout.Controls.Add(promptLabel, 0, layout.RowCount);
        layout.Controls.Add(_systemPromptBox, 1, layout.RowCount);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _systemPromptBox.Margin = new Padding(0, 4, 0, 4);
        _systemPromptBox.MinimumSize = new Size(0, 120);
        layout.RowCount++;

        AddRow(layout, "Provider override", _providerCombo);

        // Populate the reasoning effort dropdown once (static list).
        if (_reasoningEffortCombo.Items.Count == 0)
        {
            foreach (var choice in ReasoningChoices)
            {
                _reasoningEffortCombo.Items.Add(choice.Label);
            }
        }
        _reasoningEffortCombo.SelectedIndex = 0;
        AddRow(layout, "Reasoning effort", _reasoningEffortCombo);
        var reasoningHelp = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "Inherit = use provider default. Off explicitly disables thinking on Anthropic/Ollama; OpenAI/Copilot clamp to lowest. XHigh maps to High on OpenAI/Azure.",
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 0, 0, 4),
        };
        layout.Controls.Add(new Label { Width = 0, Height = 0 }, 0, layout.RowCount);
        layout.Controls.Add(reasoningHelp, 1, layout.RowCount);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        AddRow(layout, "Picker order", _pickerOrderNumeric);
        AddRow(layout, string.Empty, _includeInPickerCheckBox);

        return layout;
    }

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _entries.Clear();

            // Populate provider choices (raw values from config).
            _providerCombo.Items.Clear();
            _providerCombo.Items.Add(NoProviderOverride);
            foreach (var name in config.Providers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                _providerCombo.Items.Add(name);
            }

            foreach (var (key, preset) in config.Presets)
            {
                _entries[key] = new PresetEntry
                {
                    OriginalKey = key,
                    CurrentKey = key,
                    Name = preset.Name,
                    SystemPrompt = preset.SystemPrompt,
                    Provider = preset.Provider,
                    PickerOrder = preset.PickerOrder ?? 0,
                    IncludeInPicker = preset.IncludeInPicker,
                    ReasoningEffort = preset.ReasoningEffort,
                };
            }

            RepopulateList();
        });
    }

    public override string? Validate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.CurrentKey))
            {
                return "Preset key cannot be empty.";
            }

            if (!seen.Add(entry.CurrentKey))
            {
                return $"Duplicate preset key '{entry.CurrentKey}'.";
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return $"Preset '{entry.CurrentKey}' must have a display name.";
            }

            if (string.IsNullOrWhiteSpace(entry.SystemPrompt))
            {
                return $"Preset '{entry.CurrentKey}' must have a non-empty system prompt.";
            }
        }

        return null;
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        // 1) Apply renames first so hotkeys-following-rename logic in
        //    ConfigEditor.RenamePreset can update binding references.
        foreach (var entry in _entries.Values)
        {
            if (!string.Equals(entry.OriginalKey, entry.CurrentKey, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(entry.OriginalKey))
            {
                editor.RenamePreset(entry.OriginalKey!, entry.CurrentKey);
                entry.OriginalKey = entry.CurrentKey;
            }
        }

        // 2) Drop presets that were removed in this session.
        var keepKeys = _entries.Values.Select(e => e.CurrentKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in editor.GetPresetNames().ToList())
        {
            if (!keepKeys.Contains(existing))
            {
                editor.RemovePreset(existing);
            }
        }

        // 3) Write each entry.
        foreach (var entry in _entries.Values)
        {
            var preset = new PromptPreset
            {
                Name = entry.Name,
                SystemPrompt = entry.SystemPrompt,
                Provider = string.IsNullOrEmpty(entry.Provider) ? null : entry.Provider,
                PickerOrder = entry.PickerOrder,
                IncludeInPicker = entry.IncludeInPicker,
                ReasoningEffort = entry.ReasoningEffort,
            };
            editor.SetPreset(entry.CurrentKey, preset);
        }
    }

    private void RepopulateList()
    {
        _suppressEvents = true;
        try
        {
            var previousKey = _selectedKey;
            _list.Items.Clear();
            foreach (var entry in _entries.Values.OrderBy(e => e.CurrentKey, StringComparer.OrdinalIgnoreCase))
            {
                _list.Items.Add(entry.CurrentKey);
            }

            if (previousKey != null && _entries.ContainsKey(previousKey))
            {
                _list.SelectedItem = previousKey;
            }
            else if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
            else
            {
                _selectedKey = null;
                ShowDetailFor(null);
            }
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void OnSelectionChanged()
    {
        if (_suppressEvents)
        {
            return;
        }

        _selectedKey = _list.SelectedItem as string;
        var entry = _selectedKey != null && _entries.TryGetValue(_selectedKey, out var e) ? e : null;
        _removeButton.Enabled = entry != null;
        _duplicateButton.Enabled = entry != null;
        ShowDetailFor(entry);
    }

    private void ShowDetailFor(PresetEntry? entry)
    {
        _suppressEvents = true;
        try
        {
            if (entry == null)
            {
                _detailHeader.Text = "(no preset selected)";
                _keyBox.Text = string.Empty;
                _nameBox.Text = string.Empty;
                _systemPromptBox.Text = string.Empty;
                _pickerOrderNumeric.Value = 0;
                _includeInPickerCheckBox.Checked = false;
                _providerCombo.SelectedItem = NoProviderOverride;
                _reasoningEffortCombo.SelectedIndex = 0;
                SetDetailEnabled(false);
                return;
            }

            SetDetailEnabled(true);
            _detailHeader.Text = entry.CurrentKey;
            _keyBox.Text = entry.CurrentKey;
            _nameBox.Text = entry.Name;
            _systemPromptBox.Text = entry.SystemPrompt;
            _pickerOrderNumeric.Value = Math.Max(0, Math.Min(9999, entry.PickerOrder));
            _includeInPickerCheckBox.Checked = entry.IncludeInPicker;

            // Reasoning effort selection.
            var reasoningIdx = 0;
            for (int i = 0; i < ReasoningChoices.Length; i++)
            {
                if (Equals(ReasoningChoices[i].Value, entry.ReasoningEffort))
                {
                    reasoningIdx = i;
                    break;
                }
            }
            _reasoningEffortCombo.SelectedIndex = reasoningIdx;

            if (string.IsNullOrEmpty(entry.Provider))
            {
                _providerCombo.SelectedItem = NoProviderOverride;
            }
            else if (_providerCombo.Items.Contains(entry.Provider))
            {
                _providerCombo.SelectedItem = entry.Provider;
            }
            else
            {
                _providerCombo.Items.Add(entry.Provider);
                _providerCombo.SelectedItem = entry.Provider;
            }
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void SetDetailEnabled(bool enabled)
    {
        _keyBox.Enabled = enabled;
        _nameBox.Enabled = enabled;
        _systemPromptBox.Enabled = enabled;
        _providerCombo.Enabled = enabled;
        _reasoningEffortCombo.Enabled = enabled;
        _pickerOrderNumeric.Enabled = enabled;
        _includeInPickerCheckBox.Enabled = enabled;
    }

    private void OnFieldEdited(Action<PresetEntry> apply)
    {
        if (_suppressEvents || _selectedKey == null || !_entries.TryGetValue(_selectedKey, out var entry))
        {
            return;
        }

        apply(entry);
        MarkDirty();
    }

    private void OnKeyEdited()
    {
        if (_suppressEvents || _selectedKey == null || !_entries.TryGetValue(_selectedKey, out var entry))
        {
            return;
        }

        var newKey = _keyBox.Text.Trim();
        if (string.IsNullOrEmpty(newKey) || string.Equals(newKey, entry.CurrentKey, StringComparison.Ordinal))
        {
            return;
        }

        if (_entries.ContainsKey(newKey))
        {
            // Don't allow a typed-in collision; the user must remove the
            // other preset first or pick a different name. The Validate()
            // step will surface this on save attempt.
            return;
        }

        _entries.Remove(entry.CurrentKey);
        entry.CurrentKey = newKey;
        _entries[newKey] = entry;
        _selectedKey = newKey;
        MarkDirty();
        RepopulateList();
        _list.SelectedItem = newKey;
        _detailHeader.Text = newKey;
    }

    private void OnAddClicked()
    {
        var newKey = MakeUniqueKey("preset");
        var entry = new PresetEntry
        {
            CurrentKey = newKey,
            Name = "New preset",
            SystemPrompt = "Describe how the AI should transform the selected text.",
            IncludeInPicker = true,
            PickerOrder = NextPickerOrder(),
        };
        _entries[newKey] = entry;
        MarkDirty();
        RepopulateList();
        _list.SelectedItem = newKey;
        _nameBox.SelectAll();
        _nameBox.Focus();
    }

    private void OnDuplicateClicked()
    {
        if (_selectedKey == null || !_entries.TryGetValue(_selectedKey, out var source))
        {
            return;
        }

        var newKey = MakeUniqueKey($"{source.CurrentKey}-copy");
        var entry = new PresetEntry
        {
            CurrentKey = newKey,
            Name = $"{source.Name} (copy)",
            SystemPrompt = source.SystemPrompt,
            Provider = source.Provider,
            PickerOrder = source.PickerOrder,
            IncludeInPicker = source.IncludeInPicker,
            ReasoningEffort = source.ReasoningEffort,
        };
        _entries[newKey] = entry;
        MarkDirty();
        RepopulateList();
        _list.SelectedItem = newKey;
    }

    private void OnRemoveClicked()
    {
        if (_selectedKey == null || !_entries.ContainsKey(_selectedKey))
        {
            return;
        }

        var key = _selectedKey;
        var result = MessageBox.Show(
            this,
            $"Remove preset '{key}'?\n\nHotkeys that referenced it will become invalid until you rebind them.",
            "Remove preset",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (result != DialogResult.OK)
        {
            return;
        }

        _entries.Remove(key);
        MarkDirty();
        RepopulateList();
    }

    private string MakeUniqueKey(string baseKey)
    {
        var key = baseKey;
        var i = 2;
        while (_entries.ContainsKey(key))
        {
            key = $"{baseKey}-{i}";
            i++;
        }
        return key;
    }

    private int NextPickerOrder()
    {
        if (_entries.Count == 0)
        {
            return 1;
        }
        return _entries.Values.Max(e => e.PickerOrder) + 1;
    }

    private void AddRow(TableLayoutPanel layout, string label, Control control)
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

    private sealed class PresetEntry
    {
        /// <summary>
        /// Key the preset had when it was loaded; <c>null</c> for new entries.
        /// Tracked separately so renames can update hotkey references.
        /// </summary>
        public string? OriginalKey { get; set; }

        public string CurrentKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string? Provider { get; set; }
        public int PickerOrder { get; set; }
        public bool IncludeInPicker { get; set; } = true;
        public ReasoningEffort? ReasoningEffort { get; set; }
    }
}
