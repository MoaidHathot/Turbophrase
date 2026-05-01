using Turbophrase.Core.Configuration;
using Turbophrase.Services;

namespace Turbophrase.Settings.Tabs;

/// <summary>
/// Master/detail editor for the <c>providers</c> section of
/// <c>turbophrase.json</c>. Edits are kept in-memory until the surrounding
/// <see cref="SettingsForm"/> saves; secret values written into the API key
/// field can optionally be persisted to Windows Credential Manager and
/// replaced in the JSON with an <c>@credman:NAME</c> reference.
/// </summary>
public sealed class ProvidersTab : SettingsTabBase
{
    private static readonly string[] KnownTypes =
    {
        "openai",
        "azure-openai",
        "anthropic",
        "ollama",
        "copilot",
    };

    private readonly ListBox _providersList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
    };
    private readonly Button _addButton = new() { Text = "Add...", AutoSize = true };
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true, Enabled = false };
    private readonly Button _setDefaultButton = new() { Text = "Set as default", AutoSize = true, Enabled = false };

    private readonly Label _detailHeader = new()
    {
        AutoSize = true,
        Font = new Font(SystemFonts.DefaultFont!.FontFamily, 11f, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly Label _typeLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _apiKeyBox = new() { UseSystemPasswordChar = true };
    private readonly CheckBox _showApiKey = new() { Text = "Show", AutoSize = true };
    private readonly CheckBox _saveInCredMan = new() { Text = "Save in Windows Credential Manager", AutoSize = true };
    private readonly TextBox _endpointBox = new();
    private readonly TextBox _modelBox = new();
    private readonly TextBox _deploymentBox = new();
    private readonly Label _credManHint = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
    };

    private readonly Button _testButton = new() { Text = "Test connection", AutoSize = true };
    private readonly Label _testResultLabel = new()
    {
        AutoSize = true,
        AutoEllipsis = true,
        MaximumSize = new Size(420, 0),
    };

    private readonly Dictionary<string, ProviderEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedName;
    private string? _initialDefaultProvider;
    private string? _currentDefaultProvider;
    private bool _suppressEvents;
    private SecretsStore? _secretsStore;

    public ProvidersTab()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 220,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = false,
        };

        // Left panel: provider list + buttons
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftLayout.Controls.Add(_providersList, 0, 0);

        var leftButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 0),
        };
        leftButtons.Controls.Add(_addButton);
        leftButtons.Controls.Add(_removeButton);
        leftButtons.Controls.Add(_setDefaultButton);
        leftLayout.Controls.Add(leftButtons, 0, 1);

        split.Panel1.Controls.Add(leftLayout);
        split.Panel1.Padding = new Padding(0, 0, 8, 0);

        // Right panel: detail editor
        split.Panel2.Padding = new Padding(8, 0, 0, 0);
        split.Panel2.Controls.Add(BuildDetailPanel());

        Controls.Add(split);

        _providersList.SelectedIndexChanged += (_, _) => OnSelectionChanged();
        _providersList.DrawMode = DrawMode.OwnerDrawFixed;
        _providersList.ItemHeight = 22;
        _providersList.DrawItem += OnDrawProviderItem;

        _addButton.Click += (_, _) => OnAddClicked();
        _removeButton.Click += (_, _) => OnRemoveClicked();
        _setDefaultButton.Click += (_, _) => OnSetDefaultClicked();
        _testButton.Click += async (_, _) => await OnTestClickedAsync();

        _showApiKey.CheckedChanged += (_, _) =>
        {
            _apiKeyBox.UseSystemPasswordChar = !_showApiKey.Checked;
        };

        _apiKeyBox.TextChanged += (_, _) => OnFieldEdited(e => e.ApiKey = _apiKeyBox.Text);
        _saveInCredMan.CheckedChanged += (_, _) =>
        {
            OnFieldEdited(e => e.SaveApiKeyInCredMan = _saveInCredMan.Checked);
            UpdateCredManHint();
        };
        _endpointBox.TextChanged += (_, _) => OnFieldEdited(e => e.Endpoint = _endpointBox.Text);
        _modelBox.TextChanged += (_, _) => OnFieldEdited(e => e.Model = _modelBox.Text);
        _deploymentBox.TextChanged += (_, _) => OnFieldEdited(e => e.DeploymentName = _deploymentBox.Text);
    }

    private SecretsStore EnsureSecretsStore() => _secretsStore ??= new SecretsStore();

    public override string Title => "Providers";

    private Control BuildDetailPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        layout.Controls.Add(_detailHeader, 0, 0);
        layout.SetColumnSpan(_detailHeader, 2);

        AddRow(layout, "Type", _typeLabel);

        // API key row with Show + Save-in-CredMan checkboxes
        var apiKeyHost = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        apiKeyHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        apiKeyHost.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _apiKeyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _apiKeyBox.Width = 360;
        apiKeyHost.Controls.Add(_apiKeyBox, 0, 0);
        apiKeyHost.Controls.Add(_showApiKey, 1, 0);
        AddRow(layout, "API key", apiKeyHost);

        AddRow(layout, string.Empty, _saveInCredMan);
        AddRow(layout, string.Empty, _credManHint);

        _endpointBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _endpointBox.Width = 420;
        AddRow(layout, "Endpoint", _endpointBox);

        _modelBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _modelBox.Width = 320;
        AddRow(layout, "Model", _modelBox);

        _deploymentBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _deploymentBox.Width = 320;
        AddRow(layout, "Deployment name", _deploymentBox);

        var testPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
        };
        testPanel.Controls.Add(_testButton);
        testPanel.Controls.Add(_testResultLabel);
        AddRow(layout, string.Empty, testPanel);

        return layout;
    }

    public override void LoadFrom(TurbophraseConfig config)
    {
        WithoutDirty(() =>
        {
            _entries.Clear();
            _initialDefaultProvider = config.DefaultProvider;
            _currentDefaultProvider = config.DefaultProvider;

            // Read raw (un-resolved) values straight from the JSON so that
            // ${ENV} and @credman: references are visible to the user as-is.
            try
            {
                var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);
                foreach (var name in editor.GetProviderNames())
                {
                    var entry = new ProviderEntry
                    {
                        Name = name,
                        Type = editor.GetProviderRawField(name, "type") ?? string.Empty,
                        ApiKey = editor.GetProviderRawField(name, "apiKey"),
                        Endpoint = editor.GetProviderRawField(name, "endpoint"),
                        Model = editor.GetProviderRawField(name, "model"),
                        DeploymentName = editor.GetProviderRawField(name, "deploymentName"),
                    };
                    entry.OriginalApiKey = entry.ApiKey;
                    _entries[name] = entry;
                }
            }
            catch
            {
                // Fall back to in-memory config (resolved values) -- still
                // usable, just exposes resolved secrets if any.
                foreach (var (name, p) in config.Providers)
                {
                    _entries[name] = new ProviderEntry
                    {
                        Name = name,
                        Type = p.Type,
                        ApiKey = p.ApiKey,
                        Endpoint = p.Endpoint,
                        Model = p.Model,
                        DeploymentName = p.DeploymentName,
                    };
                }
            }

            RepopulateList(preserveSelection: false);
        });
    }

    public override string? Validate()
    {
        foreach (var entry in _entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Type))
            {
                return $"Provider '{entry.Name}' is missing a type.";
            }

            if (Array.IndexOf(KnownTypes, entry.Type) < 0)
            {
                // Unknown types are allowed (forward-compat) but warn anyway.
                continue;
            }

            if (RequiresApiKey(entry.Type) && string.IsNullOrWhiteSpace(entry.ApiKey))
            {
                return $"Provider '{entry.Name}' requires an API key.";
            }

            if (entry.Type == "azure-openai")
            {
                if (string.IsNullOrWhiteSpace(entry.Endpoint))
                {
                    return $"Provider '{entry.Name}' requires an endpoint.";
                }

                if (string.IsNullOrWhiteSpace(entry.DeploymentName))
                {
                    return $"Provider '{entry.Name}' requires a deployment name.";
                }
            }

            if (entry.Type == "ollama" && string.IsNullOrWhiteSpace(entry.Endpoint))
            {
                return $"Provider '{entry.Name}' requires an endpoint.";
            }
        }

        if (string.IsNullOrEmpty(_currentDefaultProvider) && _entries.Count > 0)
        {
            return "A default provider must be selected.";
        }

        if (!string.IsNullOrEmpty(_currentDefaultProvider) && !_entries.ContainsKey(_currentDefaultProvider!))
        {
            return $"Default provider '{_currentDefaultProvider}' is not defined.";
        }

        return null;
    }

    public override void ApplyTo(ConfigEditor editor)
    {
        // Remove providers that were deleted in this session.
        foreach (var existing in editor.GetProviderNames())
        {
            if (!_entries.ContainsKey(existing))
            {
                editor.RemoveProvider(existing);
            }
        }

        foreach (var entry in _entries.Values)
        {
            var apiKeyValue = ResolveApiKeyForSave(entry);

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = entry.Type,
                ["apiKey"] = apiKeyValue,
                ["endpoint"] = NormalizeOptional(entry.Endpoint),
                ["model"] = NormalizeOptional(entry.Model),
                ["deploymentName"] = NormalizeOptional(entry.DeploymentName),
            };
            editor.SetProviderFields(entry.Name, fields);
        }

        if (!string.IsNullOrEmpty(_currentDefaultProvider) && _currentDefaultProvider != _initialDefaultProvider)
        {
            editor.SetDefaultProvider(_currentDefaultProvider!);
        }
    }

    private string? ResolveApiKeyForSave(ProviderEntry entry)
    {
        if (!RequiresApiKey(entry.Type))
        {
            return NormalizeOptional(entry.ApiKey);
        }

        var trimmed = entry.ApiKey?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        // Already a reference -- leave alone, even if "Save in CredMan" is
        // checked. Toggling the checkbox on a reference is a no-op for the
        // file; the user should clear and re-enter to migrate.
        if (IsReference(trimmed))
        {
            return trimmed;
        }

        if (entry.SaveApiKeyInCredMan)
        {
            try
            {
                var credName = $"{entry.Name}:apiKey";
                EnsureSecretsStore().Save(credName, trimmed);
                return ConfigurationService.CredManPrefix + credName;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to save the API key for '{entry.Name}' to Credential Manager: {ex.Message}", ex);
            }
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim();
    }

    private static bool IsReference(string value)
    {
        return value.StartsWith(ConfigurationService.CredManPrefix, StringComparison.Ordinal)
            || (value.Contains("${", StringComparison.Ordinal) && value.Contains('}'));
    }

    private static bool RequiresApiKey(string type) =>
        type is "openai" or "azure-openai" or "anthropic";

    private void RepopulateList(bool preserveSelection)
    {
        var previousName = preserveSelection ? _selectedName : null;
        _suppressEvents = true;
        try
        {
            _providersList.Items.Clear();
            foreach (var name in _entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                _providersList.Items.Add(name);
            }
        }
        finally
        {
            _suppressEvents = false;
        }

        if (previousName != null && _entries.ContainsKey(previousName))
        {
            _providersList.SelectedItem = previousName;
        }
        else if (_providersList.Items.Count > 0)
        {
            _providersList.SelectedIndex = 0;
        }
        else
        {
            _selectedName = null;
            ShowDetailFor(null);
        }
    }

    private void OnSelectionChanged()
    {
        if (_suppressEvents)
        {
            return;
        }

        _selectedName = _providersList.SelectedItem as string;
        var hasSelection = _selectedName != null && _entries.ContainsKey(_selectedName);
        _removeButton.Enabled = hasSelection;
        _setDefaultButton.Enabled = hasSelection && _selectedName != _currentDefaultProvider;
        ShowDetailFor(hasSelection ? _entries[_selectedName!] : null);
    }

    private void ShowDetailFor(ProviderEntry? entry)
    {
        _suppressEvents = true;
        try
        {
            if (entry == null)
            {
                _detailHeader.Text = "(no provider selected)";
                _typeLabel.Text = string.Empty;
                _apiKeyBox.Text = string.Empty;
                _endpointBox.Text = string.Empty;
                _modelBox.Text = string.Empty;
                _deploymentBox.Text = string.Empty;
                _saveInCredMan.Checked = false;
                _testResultLabel.Text = string.Empty;
                SetDetailEnabled(false);
                return;
            }

            SetDetailEnabled(true);
            var defaultBadge = entry.Name == _currentDefaultProvider ? "  (default)" : string.Empty;
            _detailHeader.Text = $"{entry.Name}{defaultBadge}";
            _typeLabel.Text = string.IsNullOrEmpty(entry.Type) ? "(unknown)" : entry.Type;
            _apiKeyBox.Text = entry.ApiKey ?? string.Empty;
            _endpointBox.Text = entry.Endpoint ?? string.Empty;
            _modelBox.Text = entry.Model ?? string.Empty;
            _deploymentBox.Text = entry.DeploymentName ?? string.Empty;
            _saveInCredMan.Checked = entry.SaveApiKeyInCredMan;
            _testResultLabel.Text = string.Empty;
            _testResultLabel.ForeColor = SystemColors.ControlText;

            var requiresKey = RequiresApiKey(entry.Type);
            _apiKeyBox.Enabled = requiresKey;
            _showApiKey.Enabled = requiresKey;
            _saveInCredMan.Enabled = requiresKey;
            _endpointBox.Enabled = entry.Type is "azure-openai" or "ollama";
            _deploymentBox.Enabled = entry.Type == "azure-openai";
            UpdateCredManHint();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void UpdateCredManHint()
    {
        if (_selectedName == null || !_entries.TryGetValue(_selectedName, out var entry))
        {
            _credManHint.Text = string.Empty;
            return;
        }

        if (!RequiresApiKey(entry.Type))
        {
            _credManHint.Text = string.Empty;
            return;
        }

        if (IsReference(entry.ApiKey ?? string.Empty))
        {
            _credManHint.Text = entry.ApiKey!.StartsWith(ConfigurationService.CredManPrefix, StringComparison.Ordinal)
                ? "Stored in Windows Credential Manager."
                : "Resolved from environment variable.";
            return;
        }

        if (_saveInCredMan.Checked)
        {
            _credManHint.Text = $"Will be saved as {ConfigurationService.CredManPrefix}{entry.Name}:apiKey on Save.";
        }
        else
        {
            _credManHint.Text = "Stored as plain text in turbophrase.json. Tick the box above to move it to Credential Manager.";
        }
    }

    private void SetDetailEnabled(bool enabled)
    {
        _apiKeyBox.Enabled = enabled;
        _showApiKey.Enabled = enabled;
        _saveInCredMan.Enabled = enabled;
        _endpointBox.Enabled = enabled;
        _modelBox.Enabled = enabled;
        _deploymentBox.Enabled = enabled;
        _testButton.Enabled = enabled;
    }

    private void OnFieldEdited(Action<ProviderEntry> apply)
    {
        if (_suppressEvents || _selectedName == null || !_entries.TryGetValue(_selectedName, out var entry))
        {
            return;
        }

        apply(entry);
        MarkDirty();
        UpdateCredManHint();
    }

    private void OnAddClicked()
    {
        using var dialog = new AddProviderDialog(KnownTypes, _entries.Keys);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var entry = new ProviderEntry
        {
            Name = dialog.ProviderName,
            Type = dialog.ProviderType,
        };
        _entries[entry.Name] = entry;

        if (string.IsNullOrEmpty(_currentDefaultProvider))
        {
            _currentDefaultProvider = entry.Name;
        }

        MarkDirty();
        RepopulateList(preserveSelection: false);
        _providersList.SelectedItem = entry.Name;
    }

    private void OnRemoveClicked()
    {
        if (_selectedName == null || !_entries.ContainsKey(_selectedName))
        {
            return;
        }

        var name = _selectedName;
        var result = MessageBox.Show(
            this,
            $"Remove provider '{name}' from configuration?\n\nThis does not delete any saved Credential Manager entries.",
            "Remove provider",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (result != DialogResult.OK)
        {
            return;
        }

        _entries.Remove(name);
        if (_currentDefaultProvider == name)
        {
            _currentDefaultProvider = _entries.Keys.FirstOrDefault();
        }

        MarkDirty();
        RepopulateList(preserveSelection: false);
    }

    private void OnSetDefaultClicked()
    {
        if (_selectedName == null)
        {
            return;
        }

        _currentDefaultProvider = _selectedName;
        MarkDirty();
        _providersList.Invalidate();
        ShowDetailFor(_entries[_selectedName]);
        _setDefaultButton.Enabled = false;
    }

    private async Task OnTestClickedAsync()
    {
        if (_selectedName == null || !_entries.TryGetValue(_selectedName, out var entry))
        {
            return;
        }

        _testButton.Enabled = false;
        _testResultLabel.ForeColor = SystemColors.GrayText;
        _testResultLabel.Text = "Testing...";
        try
        {
            // Build a ProviderConfig with secrets resolved exactly as the
            // running app would see them. We do NOT save first -- this lets
            // the user iterate without writing every change.
            var probe = new ProviderConfig
            {
                Type = entry.Type,
                ApiKey = ConfigurationService.ResolveSecretReference(entry.ApiKey),
                Endpoint = ConfigurationService.ResolveSecretReference(entry.Endpoint),
                Model = ConfigurationService.ResolveSecretReference(entry.Model),
                DeploymentName = ConfigurationService.ResolveSecretReference(entry.DeploymentName),
            };

            var result = await ProviderTester.TestAsync(entry.Name, probe);
            if (result.Success)
            {
                _testResultLabel.ForeColor = Color.SeaGreen;
                _testResultLabel.Text = $"OK ({result.Elapsed.TotalSeconds:F1}s): {Trim(result.Response, 80)}";
            }
            else
            {
                _testResultLabel.ForeColor = Color.Firebrick;
                _testResultLabel.Text = $"Failed: {Trim(result.ErrorMessage, 200)}";
            }
        }
        finally
        {
            _testButton.Enabled = true;
        }
    }

    private static string Trim(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        return text.Length > max ? text[..max] + "..." : text;
    }

    private void OnDrawProviderItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _providersList.Items.Count)
        {
            return;
        }

        var name = (string)_providersList.Items[e.Index]!;
        var isDefault = name == _currentDefaultProvider;
        var label = isDefault ? $"{name}  (default)" : name;
        TextRenderer.DrawText(
            e.Graphics,
            label,
            e.Font ?? _providersList.Font,
            e.Bounds,
            (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : e.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
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
        layout.RowCount++;
    }

    private sealed class ProviderEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? OriginalApiKey { get; set; }
        public string? Endpoint { get; set; }
        public string? Model { get; set; }
        public string? DeploymentName { get; set; }
        public bool SaveApiKeyInCredMan { get; set; }
    }

    /// <summary>
    /// Modal dialog for adding a new provider entry.
    /// </summary>
    private sealed class AddProviderDialog : Form
    {
        private readonly TextBox _nameBox = new() { Width = 220 };
        private readonly ComboBox _typeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private readonly Label _error = new() { ForeColor = Color.Firebrick, AutoSize = true, MaximumSize = new Size(360, 0) };
        private readonly HashSet<string> _existingNames;

        public string ProviderName { get; private set; } = string.Empty;
        public string ProviderType { get; private set; } = string.Empty;

        public AddProviderDialog(IEnumerable<string> types, IEnumerable<string> existingNames)
        {
            Text = "Add provider";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 200);
            ShowInTaskbar = false;

            _existingNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

            foreach (var t in types)
            {
                _typeBox.Items.Add(t);
            }
            if (_typeBox.Items.Count > 0)
            {
                _typeBox.SelectedIndex = 0;
            }

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            layout.Controls.Add(new Label { Text = "Name", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 4) }, 0, 0);
            layout.Controls.Add(_nameBox, 1, 0);
            layout.Controls.Add(new Label { Text = "Type", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 4) }, 0, 1);
            layout.Controls.Add(_typeBox, 1, 1);
            layout.Controls.Add(_error, 1, 2);

            var ok = new Button { Text = "Add", DialogResult = DialogResult.None, Width = 90 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(12),
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            ok.Click += (_, _) =>
            {
                var name = _nameBox.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    _error.Text = "Name is required.";
                    return;
                }
                if (_existingNames.Contains(name))
                {
                    _error.Text = $"A provider named '{name}' already exists.";
                    return;
                }
                if (_typeBox.SelectedItem is not string type || string.IsNullOrEmpty(type))
                {
                    _error.Text = "Type is required.";
                    return;
                }

                ProviderName = name;
                ProviderType = type;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
