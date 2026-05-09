using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Turbophrase.Avalonia;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;
using AApplication = Avalonia.Application;
using ABrushes = Avalonia.Media.Brushes;
using AButton = Avalonia.Controls.Button;
using ACheckBox = Avalonia.Controls.CheckBox;
using AControl = Avalonia.Controls.Control;
using AHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using AOrientation = Avalonia.Layout.Orientation;
using ATextBox = Avalonia.Controls.TextBox;
using AThickness = Avalonia.Thickness;

namespace Turbophrase.Avalonia.Windows;

public sealed class FirstRunWindow : Window
{
    private const double WideLayoutThreshold = 760;

    private static readonly ProviderOption[] ProviderOptions =
    [
        new("openai", "OpenAI", "gpt-4o-mini", string.Empty, true, false,
            "Fast hosted models from OpenAI.",
            "Paste an API key from platform.openai.com."),
        new("anthropic", "Anthropic Claude", "claude-sonnet-4-20250514", string.Empty, true, false,
            "Claude models for careful rewriting and tone work.",
            "Paste an API key from console.anthropic.com."),
        new("azure-openai", "Azure OpenAI", "gpt-4o-mini", string.Empty, true, true,
            "Use an Azure OpenAI resource or Foundry chat completions endpoint.",
            "Enter your endpoint and API key. The model field can be your deployment name."),
        new("ollama", "Ollama", "llama3.2", "http://localhost:11434", false, true,
            "Run transformations locally with an Ollama model.",
            "Install Ollama, pull a model, and keep the local service running."),
        new("copilot", "GitHub Copilot", "gpt-5-mini", string.Empty, false, false,
            "Use your signed-in GitHub Copilot account. No API key needed.",
            "Turbophrase uses the bundled Copilot integration and your existing Copilot auth."),
    ];

    private readonly ContentControl _contentHost = new();
    private readonly TextBlock _stepText = new() { Classes = { "muted" }, FontSize = 12 };
    private readonly TextBlock _title = new() { Classes = { "title" } };
    private readonly TextBlock _subtitle = new() { Classes = { "muted" }, FontSize = 13 };
    private readonly TextBlock _status = new() { Classes = { "muted" }, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _progressHost = new();
    private readonly Grid _body = new();
    private readonly AButton _backButton = new() { Content = "Back", MinWidth = 92 };
    private readonly AButton _nextButton = new() { Classes = { "primary" }, Content = "Next", MinWidth = 104 };
    private readonly AButton _cancelButton = new() { Content = "Cancel", MinWidth = 92 };

    private int _step;
    private ProviderOption? _chosen;
    private string _apiKey = string.Empty;
    private string _endpoint = string.Empty;
    private string _model = string.Empty;
    private bool _saveInCredMan = true;
    private bool _testing;
    private bool _testSucceeded;
    private string? _testResult;

    public FirstRunWindow()
    {
        Title = "Welcome to Turbophrase";
        RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark;
        SetInitialWindowBounds();
        MinWidth = 520;
        MinHeight = 480;
        CanResize = true;
        ShowInTaskbar = true;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.Mica, WindowTransparencyLevel.None];
        Background = ABrushes.Transparent;

        _backButton.Click += (_, _) => GoToStep(_step - 1);
        _nextButton.Click += async (_, _) => await OnNextClicked();
        _cancelButton.Click += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        ChooseProvider(ProviderOptions[0], rebuild: false);
        Content = BuildShell();
        SizeChanged += (_, _) => UpdateAdaptiveLayout();
        GoToStep(0);
    }

    public bool Accepted { get; private set; }

    public static async Task<bool> ShowProviderSetupAsync()
    {
        FirstRunWindow? window = null;
        await AvaloniaUiHost.ShowStandaloneWindowAsync(() => window = new FirstRunWindow());
        return window?.Accepted == true;
    }

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

        foreach (var (_, provider) in config.Providers)
        {
            switch (provider.Type?.ToLowerInvariant())
            {
                case "openai":
                case "anthropic":
                    if (HasUsableValue(provider.ApiKey)) return false;
                    break;
                case "azure-openai":
                    if (HasUsableValue(provider.ApiKey) && HasUsableValue(provider.Endpoint)) return false;
                    break;
                case "ollama":
                    if (HasUsableValue(provider.Endpoint)) return false;
                    break;
                case "copilot":
                case "copilot-cli":
                case "github-copilot":
                    return false;
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

        var trimmed = value.Trim();
        return !(trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal));
    }

    private AControl BuildShell()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = Brush("TpAcrylicTintBrush")
        };

        var hero = new Border
        {
            Background = Brush("TpAcrylicHeaderBrush"),
            BorderBrush = Brush("TpStrokeBrush"),
            BorderThickness = new AThickness(0, 0, 0, 1),
            Padding = new AThickness(30, 22),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 5,
                        Children =
                        {
                            new TextBlock { Text = "Set up Turbophrase", FontSize = 26, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = "Choose a provider once. You can refine everything later in Settings.", Classes = { "muted" }, FontSize = 13 }
                        }
                    },
                    WithColumn(_stepText, 1)
                }
            }
        };
        root.Children.Add(hero);

        _body.ColumnDefinitions = new ColumnDefinitions("220,*");
        _body.RowDefinitions = new RowDefinitions("*");
        _body.ColumnSpacing = 18;
        _body.RowSpacing = 14;
        _body.Margin = new AThickness(22);
        _body.Children.Add(_progressHost);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14
        };
        Grid.SetColumn(right, 1);
        _body.Children.Add(right);

        right.Children.Add(new Border
        {
            Classes = { "card" },
            Padding = new AThickness(22, 18),
            Child = new StackPanel
            {
                Spacing = 3,
                Children = { _title, _subtitle }
            }
        });

        var scroll = new ScrollViewer
        {
            Content = _contentHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        right.Children.Add(scroll);

        Grid.SetRow(_body, 1);
        root.Children.Add(_body);

        var footer = new Border
        {
            Classes = { "card" },
            Margin = new AThickness(22, 0, 22, 22),
            Padding = new AThickness(18, 13),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    _status,
                    WithColumn(new StackPanel
                    {
                        Orientation = AOrientation.Horizontal,
                        Spacing = 10,
                        Children = { _backButton, _nextButton, _cancelButton }
                    }, 1)
                }
            }
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        UpdateAdaptiveLayout();
        return root;
    }

    private void SetInitialWindowBounds()
    {
        var workingArea = Screens.Primary?.WorkingArea;
        if (workingArea == null)
        {
            Width = 900;
            Height = 660;
            return;
        }

        Width = Math.Clamp(workingArea.Value.Width * 0.72, 520, 920);
        Height = Math.Clamp(workingArea.Value.Height * 0.78, 480, 700);
    }

    private void UpdateAdaptiveLayout()
    {
        if (_body.Children.Count < 2)
        {
            return;
        }

        var right = _body.Children[1];
        var isNarrow = Bounds.Width < WideLayoutThreshold;
        _body.ColumnDefinitions = new ColumnDefinitions(isNarrow ? "*" : "220,*");
        _body.RowDefinitions = new RowDefinitions(isNarrow ? "Auto,*" : "*");

        Grid.SetColumn(_progressHost, 0);
        Grid.SetRow(_progressHost, 0);
        Grid.SetColumn(right, isNarrow ? 0 : 1);
        Grid.SetRow(right, isNarrow ? 1 : 0);
    }

    private AControl BuildProgressRail()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(StepPill(0, "1", "Provider", "Pick the service Turbophrase should use."));
        stack.Children.Add(StepPill(1, "2", "Credentials", "Add keys, endpoint, and model."));
        stack.Children.Add(StepPill(2, "3", "Test", "Check the connection and save."));

        return new Border
        {
            Classes = { "card" },
            Padding = new AThickness(14),
            Child = stack
        };
    }

    private AControl StepPill(int step, string number, string title, string subtitle)
    {
        var active = _step == step;
        return new Border
        {
            Classes = { "softCard" },
            Background = active ? Brush("TpAccentSoftBrush") : Brush("TpAcrylicRaisedBrush"),
            BorderBrush = active ? Brush("TpAccentBrush") : Brush("TpStrokeBrush"),
            Padding = new AThickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("34,*"),
                ColumnSpacing = 10,
                Children =
                {
                    new Border
                    {
                        Width = 28,
                        Height = 28,
                        CornerRadius = new CornerRadius(4),
                        Background = active ? Brush("TpAccentBrush") : Brush("TpAcrylicCardBrush"),
                        Child = new TextBlock
                        {
                            Text = number,
                            Foreground = active ? Brush("TpAccentTextBrush") : Brush("TpTextBrush"),
                            FontWeight = FontWeight.SemiBold,
                            HorizontalAlignment = AHorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    WithColumn(new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 13 },
                            new TextBlock { Text = subtitle, Classes = { "muted" }, FontSize = 11 }
                        }
                    }, 1)
                }
            }
        };
    }

    private void GoToStep(int step)
    {
        if (step < 0 || step > 2 || _testing)
        {
            return;
        }

        _step = step;
        RenderCurrentStep();
        UpdateButtons();
    }

    private void RenderCurrentStep()
    {
        _stepText.Text = $"Step {_step + 1} of 3";
        _progressHost.Content = BuildProgressRail();

        switch (_step)
        {
            case 0:
                _title.Text = "Choose your provider";
                _subtitle.Text = "Start with the provider you want as the default. This is not permanent.";
                _contentHost.Content = BuildProviderStep();
                SetStatus("Pick a provider, then continue.", "TpMutedTextBrush");
                break;
            case 1:
                _title.Text = "Add credentials";
                _subtitle.Text = _chosen?.SetupHint ?? "Enter the details needed by the provider.";
                _contentHost.Content = BuildCredentialsStep();
                SetStatus("Credential Manager storage is recommended for API keys.", "TpMutedTextBrush");
                break;
            case 2:
                _title.Text = "Test and finish";
                _subtitle.Text = "A failed test does not block saving. You can fix provider settings later.";
                _contentHost.Content = BuildTestStep();
                if (_testing)
                {
                    SetStatus("Testing connection...", "TpMutedTextBrush");
                }
                else if (_testSucceeded)
                {
                    SetStatus("Connection succeeded. Finish will save these settings.", "TpSuccessBrush");
                }
                else if (!string.IsNullOrWhiteSpace(_testResult))
                {
                    SetStatus("Connection test failed. You can still finish and adjust later.", "TpDangerBrush");
                }
                break;
        }
    }

    private AControl BuildProviderStep()
    {
        var stack = new StackPanel { Spacing = 12 };
        foreach (var option in ProviderOptions)
        {
            stack.Children.Add(ProviderCard(option));
        }

        return Card(stack);
    }

    private AControl ProviderCard(ProviderOption option)
    {
        var selected = _chosen?.Type == option.Type;
        var card = new Border
        {
            Classes = { "softCard" },
            Background = selected ? Brush("TpAccentSoftBrush") : Brush("TpAcrylicRaisedBrush"),
            BorderBrush = selected ? Brush("TpAccentBrush") : Brush("TpStrokeBrush"),
            Padding = new AThickness(16, 13),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = option.DisplayName, FontSize = 16, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = option.Summary, Classes = { "muted" }, FontSize = 12 },
                            new TextBlock { Text = option.RequiresApiKey ? "API key required" : "No API key required", Classes = { "subtle" }, FontSize = 11 }
                        }
                    },
                    WithColumn(Pill(selected ? "Selected" : option.Type), 1)
                }
            }
        };

        card.PointerPressed += (_, _) => ChooseProvider(option, rebuild: true);
        return card;
    }

    private AControl BuildCredentialsStep()
    {
        if (_chosen == null)
        {
            return Card(new TextBlock { Classes = { "muted" }, Text = "No provider selected." });
        }

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(SectionHeader(_chosen.DisplayName, _chosen.SetupHint));

        if (_chosen.RequiresApiKey)
        {
            var apiKeyBox = new ATextBox
            {
                Text = _apiKey,
                PasswordChar = '*',
                PlaceholderText = "Paste API key",
            };
            apiKeyBox.TextChanged += (_, _) => _apiKey = apiKeyBox.Text ?? string.Empty;

            var showKey = new ACheckBox { Content = "Show key" };
            showKey.IsCheckedChanged += (_, _) => apiKeyBox.PasswordChar = showKey.IsChecked == true ? '\0' : '*';

            stack.Children.Add(Field("API key", new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 10,
                Children = { apiKeyBox, WithColumn(showKey, 1) }
            }));

            var saveSecret = new ACheckBox
            {
                Content = "Save in Windows Credential Manager",
                IsChecked = _saveInCredMan,
            };
            saveSecret.IsCheckedChanged += (_, _) => _saveInCredMan = saveSecret.IsChecked == true;
            stack.Children.Add(saveSecret);
        }

        if (_chosen.RequiresEndpoint)
        {
            var endpointBox = new ATextBox
            {
                Text = _endpoint,
                PlaceholderText = _chosen.Type == "ollama" ? "http://localhost:11434" : "https://your-resource.openai.azure.com",
            };
            endpointBox.TextChanged += (_, _) => _endpoint = endpointBox.Text ?? string.Empty;
            stack.Children.Add(Field("Endpoint", endpointBox));
        }

        var modelBox = new ATextBox
        {
            Text = _model,
            PlaceholderText = _chosen.Type == "azure-openai" ? "Deployment name or model" : "Model",
        };
        modelBox.TextChanged += (_, _) => _model = modelBox.Text ?? string.Empty;
        stack.Children.Add(Field(_chosen.Type == "azure-openai" ? "Deployment/model" : "Model", modelBox));

        stack.Children.Add(new Border
        {
            Classes = { "softCard" },
            Padding = new AThickness(14),
            Child = new TextBlock
            {
                Classes = { "muted" },
                FontSize = 12,
                Text = _chosen.Type switch
                {
                    "copilot" => "Copilot uses your existing GitHub Copilot sign-in. If auth is missing, the test step will report it.",
                    "ollama" => "For Ollama, make sure the selected model is pulled locally before testing.",
                    "azure-openai" => "For full Foundry chat-completions URLs, Turbophrase can extract the resource and deployment automatically.",
                    _ => "You can store the API key in Credential Manager so turbophrase.json does not contain the secret."
                }
            }
        });

        return Card(stack);
    }

    private AControl BuildTestStep()
    {
        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(SectionHeader("Configuration summary", "Review what will be written to turbophrase.json."));

        stack.Children.Add(SummaryLine("Provider", _chosen?.DisplayName ?? "(none)"));
        stack.Children.Add(SummaryLine("Default provider key", _chosen?.Type ?? "(none)"));
        stack.Children.Add(SummaryLine(_chosen?.Type == "azure-openai" ? "Deployment/model" : "Model", EffectiveModel()));

        if (_chosen?.RequiresEndpoint == true)
        {
            stack.Children.Add(SummaryLine("Endpoint", string.IsNullOrWhiteSpace(_endpoint) ? "(not set)" : _endpoint.Trim()));
        }

        if (_chosen?.RequiresApiKey == true)
        {
            stack.Children.Add(SummaryLine("API key", _saveInCredMan ? "Saved in Windows Credential Manager" : "Saved as plain text"));
        }

        stack.Children.Add(TestStatusCard());

        var testAgain = new AButton { Content = _testing ? "Testing..." : "Test again", IsEnabled = !_testing };
        testAgain.Click += async (_, _) => await RunTestAsync();
        stack.Children.Add(testAgain);

        return Card(stack);
    }

    private AControl TestStatusCard()
    {
        string title;
        string detail;
        string brush;

        if (_testing)
        {
            title = "Testing connection";
            detail = "Sending a short request to the selected provider.";
            brush = "TpMutedTextBrush";
        }
        else if (_testSucceeded)
        {
            title = "Connection OK";
            detail = _testResult ?? "Provider responded successfully.";
            brush = "TpSuccessBrush";
        }
        else if (!string.IsNullOrWhiteSpace(_testResult))
        {
            title = "Connection failed";
            detail = _testResult!;
            brush = "TpDangerBrush";
        }
        else
        {
            title = "Not tested yet";
            detail = "The connection test will run automatically when this step opens.";
            brush = "TpMutedTextBrush";
        }

        return new Border
        {
            Classes = { "softCard" },
            Padding = new AThickness(16),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = title, Foreground = Brush(brush), FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = detail, Classes = { "muted" }, FontSize = 12 }
                }
            }
        };
    }

    private async Task OnNextClicked()
    {
        if (_testing)
        {
            return;
        }

        if (_step == 0)
        {
            if (_chosen == null)
            {
                SetStatus("Choose a provider first.", "TpDangerBrush");
                return;
            }

            GoToStep(1);
            return;
        }

        if (_step == 1)
        {
            var error = ValidateCredentials();
            if (error != null)
            {
                SetStatus(error, "TpDangerBrush");
                return;
            }

            GoToStep(2);
            await RunTestAsync();
            return;
        }

        if (_step == 2)
        {
            try
            {
                FinishAndSave();
                Accepted = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to save configuration: {ex.Message}", "TpDangerBrush");
            }
        }
    }

    private void ChooseProvider(ProviderOption option, bool rebuild)
    {
        if (_chosen?.Type != option.Type)
        {
            _apiKey = string.Empty;
            _endpoint = option.DefaultEndpoint;
            _model = option.DefaultModel;
            _testResult = null;
            _testSucceeded = false;
        }

        _chosen = option;

        if (rebuild)
        {
            RenderCurrentStep();
            UpdateButtons();
        }
    }

    private string? ValidateCredentials()
    {
        if (_chosen == null)
        {
            return "Choose a provider first.";
        }

        if (_chosen.RequiresApiKey && string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Paste the API key for the selected provider.";
        }

        if (_chosen.RequiresEndpoint && string.IsNullOrWhiteSpace(_endpoint))
        {
            return "Enter the endpoint URL for the selected provider.";
        }

        return null;
    }

    private async Task RunTestAsync()
    {
        if (_chosen == null || _testing)
        {
            return;
        }

        _testing = true;
        _testResult = null;
        _testSucceeded = false;
        RenderCurrentStep();
        UpdateButtons();

        try
        {
            var probe = new ProviderConfig
            {
                Type = _chosen.Type,
                ApiKey = string.IsNullOrWhiteSpace(_apiKey) ? null : _apiKey.Trim(),
                Endpoint = string.IsNullOrWhiteSpace(_endpoint) ? null : _endpoint.Trim(),
                Model = EffectiveModel(),
            };

            var result = await ProviderTester.TestAsync(_chosen.Type, probe);
            _testSucceeded = result.Success;
            _testResult = result.Success
                ? $"Provider responded in {result.Elapsed.TotalSeconds:F1}s."
                : result.ErrorMessage ?? "Unknown provider error.";
        }
        catch (Exception ex)
        {
            _testSucceeded = false;
            _testResult = ex.Message;
        }
        finally
        {
            _testing = false;
            RenderCurrentStep();
            UpdateButtons();
        }
    }

    private void FinishAndSave()
    {
        if (_chosen == null)
        {
            return;
        }

        var providerName = _chosen.Type;
        var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);

        string? apiKeyValue = null;
        if (_chosen.RequiresApiKey && !string.IsNullOrWhiteSpace(_apiKey))
        {
            if (_saveInCredMan)
            {
                var credentialName = $"{providerName}:apiKey";
                new SecretsStore().Save(credentialName, _apiKey.Trim());
                apiKeyValue = ConfigurationService.CredManPrefix + credentialName;
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
            ["model"] = EffectiveModel(),
            ["deploymentName"] = null,
        };

        editor.SetProviderFields(providerName, fields);
        editor.SetDefaultProvider(providerName);
        editor.Save();
    }

    private string EffectiveModel()
    {
        if (_chosen == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(_model) ? _chosen.DefaultModel : _model.Trim();
    }

    private void UpdateButtons()
    {
        _backButton.IsEnabled = _step > 0 && !_testing;
        _nextButton.Content = _step == 2 ? "Finish" : "Next";
        _nextButton.IsEnabled = !_testing && (_step != 0 || _chosen != null);
    }

    private void SetStatus(string text, string brushKey)
    {
        _status.Text = text;
        _status.Foreground = Brush(brushKey);
    }

    private static AControl Card(AControl child) => new Border
    {
        Classes = { "card" },
        Padding = new AThickness(18),
        Child = child
    };

    private static AControl SectionHeader(string title, string subtitle) => new StackPanel
    {
        Spacing = 3,
        Children =
        {
            new TextBlock { Classes = { "sectionTitle" }, Text = title },
            new TextBlock { Classes = { "muted" }, Text = subtitle, FontSize = 12 }
        }
    };

    private static AControl Field(string label, AControl control) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("150,*"),
        ColumnSpacing = 14,
        Children =
        {
            new TextBlock { Classes = { "muted" }, Text = label, VerticalAlignment = VerticalAlignment.Center },
            WithColumn(control, 1)
        }
    };

    private static AControl SummaryLine(string label, string value) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("150,*"),
        ColumnSpacing = 14,
        Children =
        {
            new TextBlock { Classes = { "muted" }, Text = label },
            WithColumn(new TextBlock { Text = value }, 1)
        }
    };

    private static AControl Pill(string text) => new Border
    {
        CornerRadius = new CornerRadius(4),
        Background = Brush("TpAcrylicControlBrush"),
        BorderBrush = Brush("TpStrokeBrush"),
        BorderThickness = new AThickness(1),
        Padding = new AThickness(10, 5),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold }
    };

    private static T WithColumn<T>(T control, int column) where T : AControl
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;

    private sealed record ProviderOption(
        string Type,
        string DisplayName,
        string DefaultModel,
        string DefaultEndpoint,
        bool RequiresApiKey,
        bool RequiresEndpoint,
        string Summary,
        string SetupHint);
}
