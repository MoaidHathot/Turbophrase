using Turbophrase.Core.Configuration;
using Turbophrase.Services;

namespace Turbophrase.Settings;

/// <summary>
/// Three-step onboarding wizard shown the first time Turbophrase runs without
/// a configured provider. Steps:
/// 1. Welcome -- pick a provider type
/// 2. Enter credentials (API key with "Save in Credential Manager" by
///    default; or for Ollama/Copilot, instructions instead of a key)
/// 3. Test connection and finish
/// </summary>
/// <remarks>
/// On Finish, writes the chosen provider as the default into
/// <c>turbophrase.json</c>, optionally storing the API key in Windows
/// Credential Manager and replacing the JSON value with an
/// <c>@credman:NAME</c> reference. Other providers in the existing config
/// are left untouched, so re-running the wizard does not destroy state.
/// </remarks>
public sealed class FirstRunWizard : Form
{
    private static readonly (string Type, string Display, string DefaultModel, bool RequiresApiKey, bool RequiresEndpoint)[] ProviderOptions =
    {
        ("openai",       "OpenAI",                  "gpt-4o-mini",                  RequiresApiKey: true,  RequiresEndpoint: false),
        ("anthropic",    "Anthropic (Claude)",      "claude-3-5-sonnet-20241022",   RequiresApiKey: true,  RequiresEndpoint: false),
        ("azure-openai", "Azure OpenAI",            "gpt-4o-mini",                  RequiresApiKey: true,  RequiresEndpoint: true),
        ("ollama",       "Ollama (local model)",    "llama3.2",                     RequiresApiKey: false, RequiresEndpoint: true),
        ("copilot",      "GitHub Copilot CLI",      "gpt-4o",                       RequiresApiKey: false, RequiresEndpoint: false),
    };

    private readonly Panel _content = new() { Dock = DockStyle.Fill, Padding = new Padding(16) };
    private readonly Label _title = new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        Padding = new Padding(16, 12, 16, 0),
        Font = new Font(SystemFonts.DefaultFont!.FontFamily, 14f, FontStyle.Bold),
    };
    private readonly Button _backButton = new() { Text = "Back", Width = 92, Enabled = false };
    private readonly Button _nextButton = new() { Text = "Next", Width = 92 };
    private readonly Button _cancelButton = new() { Text = "Cancel", Width = 92 };

    private int _step = 0;
    private (string Type, string Display, string DefaultModel, bool RequiresApiKey, bool RequiresEndpoint)? _chosen;
    private string _apiKey = string.Empty;
    private string _endpoint = string.Empty;
    private string _model = string.Empty;
    private bool _saveInCredMan = true;
    private string? _testResult;
    private bool _testSucceeded;

    public FirstRunWizard()
    {
        Text = "Welcome to Turbophrase";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(640, 460);

        Controls.Add(_content);
        Controls.Add(_title);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Padding = new Padding(16, 8, 16, 12),
            AutoSize = true,
        };
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_nextButton);
        buttons.Controls.Add(_backButton);
        Controls.Add(buttons);

        _backButton.Click += (_, _) => GoToStep(_step - 1);
        _nextButton.Click += async (_, _) => await OnNextClicked();
        CancelButton = _cancelButton;

        GoToStep(0);
    }

    /// <summary>
    /// Returns true when the configuration on disk has no usable provider --
    /// either because the file is missing entirely or because every provider
    /// is missing its required credentials. Callers (e.g.
    /// <see cref="TrayApplicationContext"/>) should show the wizard in that
    /// case before continuing the normal startup path.
    /// </summary>
    public static bool ShouldShowFor(TurbophraseConfig config)
    {
        if (config == null)
        {
            return true;
        }

        if (config.Providers.Count == 0)
        {
            return true;
        }

        foreach (var (_, p) in config.Providers)
        {
            switch (p.Type?.ToLowerInvariant())
            {
                case "openai":
                case "anthropic":
                    if (HasUsableValue(p.ApiKey)) return false;
                    break;
                case "azure-openai":
                    if (HasUsableValue(p.ApiKey) && HasUsableValue(p.Endpoint)) return false;
                    break;
                case "ollama":
                    if (HasUsableValue(p.Endpoint)) return false;
                    break;
                case "copilot":
                case "copilot-cli":
                case "github-copilot":
                    return false; // no credentials required from us
            }
        }

        return true;
    }

    private static bool HasUsableValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Unresolved env var or credman reference still counts as
        // "configured" because the user (or the secret store) supplied it.
        // Plain text "${OPENAI_API_KEY}" with no env var set is the one case
        // we want to treat as missing -- but ResolveSecretReference returned
        // the literal string in that case, which means env var lookup failed.
        // Detect by checking the raw shape: anything starting with "${" and
        // ending with "}" that survived resolution is "missing".
        var trimmed = value.Trim();
        if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private void GoToStep(int step)
    {
        if (step < 0)
        {
            return;
        }

        _step = step;
        _content.Controls.Clear();

        switch (step)
        {
            case 0:
                _title.Text = "Step 1 of 3 -- Choose your provider";
                _content.Controls.Add(BuildStep1());
                _backButton.Enabled = false;
                _nextButton.Text = "Next";
                _nextButton.Enabled = _chosen != null;
                break;

            case 1:
                _title.Text = "Step 2 of 3 -- Enter credentials";
                _content.Controls.Add(BuildStep2());
                _backButton.Enabled = true;
                _nextButton.Text = "Next";
                _nextButton.Enabled = true;
                break;

            case 2:
                _title.Text = "Step 3 of 3 -- Test and finish";
                _content.Controls.Add(BuildStep3());
                _backButton.Enabled = true;
                _nextButton.Text = "Finish";
                _nextButton.Enabled = true;
                break;
        }
    }

    private async Task OnNextClicked()
    {
        if (_step == 0)
        {
            if (_chosen == null)
            {
                MessageBox.Show(this, "Pick a provider first.", "First-run wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            GoToStep(1);
            return;
        }

        if (_step == 1)
        {
            var error = ValidateStep2();
            if (error != null)
            {
                MessageBox.Show(this, error, "First-run wizard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Run the test before showing step 3.
            _testResult = "Testing...";
            _testSucceeded = false;
            GoToStep(2);
            await RunTestAsync();
            return;
        }

        if (_step == 2)
        {
            try
            {
                FinishAndSave();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save configuration:\n\n{ex.Message}", "First-run wizard",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private Control BuildStep1()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
        };

        var intro = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            MaximumSize = new Size(580, 0),
            Text = "Turbophrase needs an AI provider to transform text. You can change this later in Settings.",
        };
        panel.Controls.Add(intro);

        foreach (var option in ProviderOptions)
        {
            var radio = new RadioButton
            {
                Text = option.Display,
                AutoSize = true,
                Tag = option,
                Margin = new Padding(0, 6, 0, 0),
                Checked = _chosen?.Type == option.Type,
            };
            radio.CheckedChanged += (_, _) =>
            {
                if (radio.Checked)
                {
                    _chosen = ((string Type, string Display, string DefaultModel, bool RequiresApiKey, bool RequiresEndpoint))radio.Tag!;
                    _model = _chosen.Value.DefaultModel;
                    _nextButton.Enabled = true;
                }
            };
            panel.Controls.Add(radio);

            var hint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                MaximumSize = new Size(560, 0),
                Margin = new Padding(24, 0, 0, 6),
                Text = HintFor(option.Type),
            };
            panel.Controls.Add(hint);
        }

        return panel;
    }

    private static string HintFor(string type) => type switch
    {
        "openai" => "Requires an API key from platform.openai.com.",
        "anthropic" => "Requires an API key from console.anthropic.com.",
        "azure-openai" => "Requires an Azure OpenAI resource endpoint, deployment name, and API key.",
        "ollama" => "Runs locally. Install Ollama from ollama.com and pull a model first.",
        "copilot" => "Uses your existing GitHub Copilot subscription via the Copilot CLI. No API key required.",
        _ => string.Empty,
    };

    private Control BuildStep2()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        if (_chosen == null)
        {
            return new Label { Text = "(no provider chosen)", AutoSize = true };
        }

        var info = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            Text = $"Configuring: {_chosen.Value.Display}",
            Font = new Font(SystemFonts.DefaultFont!.FontFamily, 10f, FontStyle.Bold),
        };
        layout.Controls.Add(info, 0, 0);
        layout.SetColumnSpan(info, 2);

        if (_chosen.Value.RequiresApiKey)
        {
            var apiKeyBox = new TextBox { Width = 380, UseSystemPasswordChar = true, Text = _apiKey };
            var showCheck = new CheckBox { Text = "Show", AutoSize = true };
            showCheck.CheckedChanged += (_, _) => apiKeyBox.UseSystemPasswordChar = !showCheck.Checked;
            apiKeyBox.TextChanged += (_, _) => _apiKey = apiKeyBox.Text;
            var keyRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
            keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            apiKeyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            keyRow.Controls.Add(apiKeyBox, 0, 0);
            keyRow.Controls.Add(showCheck, 1, 0);
            AddRow(layout, "API key", keyRow);

            var saveCheck = new CheckBox
            {
                Text = "Save in Windows Credential Manager (recommended)",
                Checked = _saveInCredMan,
                AutoSize = true,
            };
            saveCheck.CheckedChanged += (_, _) => _saveInCredMan = saveCheck.Checked;
            AddRow(layout, string.Empty, saveCheck);
        }

        if (_chosen.Value.RequiresEndpoint)
        {
            var endpointBox = new TextBox { Width = 380, Text = _endpoint };
            endpointBox.TextChanged += (_, _) => _endpoint = endpointBox.Text;
            AddRow(layout, "Endpoint", endpointBox);
        }

        var modelBox = new TextBox { Width = 280, Text = _model };
        modelBox.TextChanged += (_, _) => _model = modelBox.Text;
        AddRow(layout, "Model", modelBox);

        if (_chosen.Value.Type == "copilot")
        {
            var copilotInfo = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 12, 0, 0),
                Text = "Make sure the Copilot CLI is installed (npm i -g @anthropic-ai/copilot-cli) and you have run 'copilot auth login'. No API key is needed.",
            };
            layout.Controls.Add(copilotInfo, 0, layout.RowCount);
            layout.SetColumnSpan(copilotInfo, 2);
            layout.RowCount++;
        }

        return layout;
    }

    private string? ValidateStep2()
    {
        if (_chosen == null)
        {
            return "Pick a provider first.";
        }

        if (_chosen.Value.RequiresApiKey && string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Paste your API key.";
        }

        if (_chosen.Value.RequiresEndpoint && string.IsNullOrWhiteSpace(_endpoint))
        {
            return "Enter the endpoint URL.";
        }

        return null;
    }

    private Control BuildStep3()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        var label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            Margin = new Padding(0, 0, 0, 12),
            Text = "Testing the connection...",
        };
        if (_testSucceeded)
        {
            label.Text = "Connection succeeded. Click Finish to save your settings.";
            label.ForeColor = Color.SeaGreen;
        }
        else if (!string.IsNullOrEmpty(_testResult) && _testResult != "Testing...")
        {
            label.Text = $"Test failed: {_testResult}\n\nYou can still finish (the settings will be saved) and try again later, or click Back to fix them.";
            label.ForeColor = Color.Firebrick;
        }
        layout.Controls.Add(label);

        var summary = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 0),
            Text = BuildSummary(),
        };
        layout.Controls.Add(summary);

        return layout;
    }

    private string BuildSummary()
    {
        if (_chosen == null)
        {
            return string.Empty;
        }

        var c = _chosen.Value;
        var parts = new List<string>
        {
            $"Provider: {c.Display}",
            $"Model: {(string.IsNullOrEmpty(_model) ? c.DefaultModel : _model)}",
        };
        if (c.RequiresEndpoint)
        {
            parts.Add($"Endpoint: {_endpoint}");
        }
        if (c.RequiresApiKey)
        {
            parts.Add(_saveInCredMan
                ? "API key: stored in Windows Credential Manager"
                : "API key: stored as plain text in turbophrase.json");
        }
        return string.Join('\n', parts);
    }

    private async Task RunTestAsync()
    {
        if (_chosen == null)
        {
            return;
        }

        _nextButton.Enabled = false;
        try
        {
            var probe = new ProviderConfig
            {
                Type = _chosen.Value.Type,
                ApiKey = _apiKey,
                Endpoint = _endpoint,
                Model = _model,
            };

            var result = await ProviderTester.TestAsync(_chosen.Value.Type, probe);
            _testSucceeded = result.Success;
            _testResult = result.Success
                ? $"Got response in {result.Elapsed.TotalSeconds:F1}s."
                : result.ErrorMessage ?? "(unknown error)";
        }
        catch (Exception ex)
        {
            _testSucceeded = false;
            _testResult = ex.Message;
        }
        finally
        {
            _nextButton.Enabled = true;
            // Refresh step 3 contents now that the test has finished.
            if (_step == 2)
            {
                _content.Controls.Clear();
                _content.Controls.Add(BuildStep3());
            }
        }
    }

    private void FinishAndSave()
    {
        if (_chosen == null)
        {
            return;
        }

        var providerName = _chosen.Value.Type;
        var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);

        string? apiKeyValue = null;
        if (_chosen.Value.RequiresApiKey && !string.IsNullOrWhiteSpace(_apiKey))
        {
            if (_saveInCredMan)
            {
                var store = new SecretsStore();
                var credName = $"{providerName}:apiKey";
                store.Save(credName, _apiKey.Trim());
                apiKeyValue = ConfigurationService.CredManPrefix + credName;
            }
            else
            {
                apiKeyValue = _apiKey.Trim();
            }
        }

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = providerName,
            ["apiKey"] = apiKeyValue,
            ["endpoint"] = string.IsNullOrWhiteSpace(_endpoint) ? null : _endpoint.Trim(),
            ["model"] = string.IsNullOrWhiteSpace(_model) ? _chosen.Value.DefaultModel : _model.Trim(),
            ["deploymentName"] = null, // not used by the wizard
        };
        editor.SetProviderFields(providerName, fields);
        editor.SetDefaultProvider(providerName);
        editor.Save();
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
