using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;
using AApplication = Avalonia.Application;
using AButton = Avalonia.Controls.Button;
using ABrushes = Avalonia.Media.Brushes;
using AColor = Avalonia.Media.Color;
using AComboBox = Avalonia.Controls.ComboBox;
using AControl = Avalonia.Controls.Control;
using ACheckBox = Avalonia.Controls.CheckBox;
using AFontFamily = Avalonia.Media.FontFamily;
using AListBox = Avalonia.Controls.ListBox;
using ATextBox = Avalonia.Controls.TextBox;
using AThickness = Avalonia.Thickness;
using AOrientation = Avalonia.Layout.Orientation;

namespace Turbophrase.Avalonia.Windows;

public sealed class SettingsWindow : Window
{
    private static readonly string[] Sections =
    {
        "General",
        "Providers",
        "Presets",
        "Hotkeys",
        "Operation picker",
        "Notifications",
        "Advanced",
    };

    private static readonly string[] ProviderTypes =
    {
        "openai",
        "azure-openai",
        "anthropic",
        "ollama",
        "copilot",
    };

    private readonly AListBox _nav = new();
    private readonly ContentControl _contentHost = new();
    private readonly TextBlock _status = new() { Classes = { "muted" }, FontSize = 12 };
    private readonly TextBlock _sectionTitle = new() { Classes = { "title" } };
    private readonly TextBlock _sectionSubtitle = new() { Classes = { "muted" }, FontSize = 13 };
    private readonly AButton _saveButton = new() { Classes = { "primary" }, Content = "Save", MinWidth = 96 };
    private readonly AButton _applyButton = new() { Content = "Apply", MinWidth = 96, IsEnabled = false };

    private readonly ObservableCollection<ProviderEntry> _providers = new();
    private readonly ObservableCollection<PresetEntry> _presets = new();
    private readonly ObservableCollection<HotkeyBinding> _hotkeys = new();
    private readonly ObservableCollection<HotkeyBinding> _pickerActions = new();
    private readonly ObservableCollection<PickerEntry> _pickerRows = new();

    private string _defaultProvider = string.Empty;
    private bool _runAtStartup;
    private string _customPromptTemplate = string.Empty;
    private NotificationSettings _notifications = new();
    private LoggingSettings _logging = new();
    private bool _dirty;
    private bool _loading;

    public SettingsWindow()
    {
        Title = "Turbophrase Settings";
        Width = 1080;
        Height = 720;
        MinWidth = 960;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = AApplication.Current?.FindResource("TpAppBackground") as IBrush ?? Brush("TpVoidBrush");

        _saveButton.Click += (_, _) => SaveAndMaybeClose(close: true);
        _applyButton.Click += (_, _) => SaveAndMaybeClose(close: false);

        Content = BuildShell();
        LoadConfiguration();
        _nav.SelectedIndex = 0;
    }

    private AControl BuildShell()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = AApplication.Current?.FindResource("TpAppBackground") as IBrush ?? Brush("TpVoidBrush")
        };

        var hero = new Border
        {
            Background = AApplication.Current?.FindResource("TpHeroGradient") as IBrush ?? Brush("TpSurfaceBrush"),
            Padding = new AThickness(30, 24),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = "Turbophrase", FontSize = 28, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = "Configure providers, prompts, hotkeys, and runtime behavior.", Classes = { "muted" }, FontSize = 13 }
                        }
                    }
                }
            }
        };

        Grid.SetRow(hero, 0);
        root.Children.Add(hero);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("240,*"),
            ColumnSpacing = 20,
            Margin = new AThickness(22)
        };

        var navCard = new Border
        {
            Classes = { "card" },
            Padding = new AThickness(14),
            Child = _nav
        };
        _nav.ItemsSource = Sections;
        _nav.SelectionChanged += (_, _) => ShowSelectedSection();
        body.Children.Add(navCard);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 14
        };
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        var header = new Border
        {
            Classes = { "card" },
            Padding = new AThickness(22, 18),
            Child = new StackPanel
            {
                Spacing = 3,
                Children = { _sectionTitle, _sectionSubtitle }
            }
        };
        right.Children.Add(header);

        var scroll = new ScrollViewer
        {
            Content = _contentHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        right.Children.Add(scroll);

        var footer = new Border
        {
            Classes = { "card" },
            Padding = new AThickness(18, 13),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    _status,
                    new StackPanel
                    {
                        Orientation = AOrientation.Horizontal,
                        Spacing = 10,
                        Children =
                        {
                            _applyButton,
                            _saveButton,
                            new AButton { Content = "Close", MinWidth = 96, Command = new RelayCommand(() => Close()) }
                        }
                    }
                }
            }
        };
        Grid.SetColumn(((Grid)footer.Child!).Children[1], 1);
        Grid.SetRow(footer, 2);
        right.Children.Add(footer);

        Grid.SetRow(body, 1);
        root.Children.Add(body);
        return root;
    }

    private void LoadConfiguration()
    {
        _loading = true;
        try
        {
            var config = ConfigurationService.LoadConfiguration();
            _defaultProvider = config.DefaultProvider;
            _runAtStartup = StartupManager.IsEnabled();
            _customPromptTemplate = config.CustomPrompt.SystemPromptTemplate;
            _notifications = config.Notifications;
            _logging = config.Logging;

            _providers.Clear();
            try
            {
                var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);
                foreach (var name in editor.GetProviderNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    _providers.Add(new ProviderEntry
                    {
                        OriginalName = name,
                        Name = name,
                        Type = editor.GetProviderRawField(name, "type") ?? string.Empty,
                        ApiKey = editor.GetProviderRawField(name, "apiKey") ?? string.Empty,
                        Endpoint = editor.GetProviderRawField(name, "endpoint") ?? string.Empty,
                        Model = editor.GetProviderRawField(name, "model") ?? string.Empty,
                        DeploymentName = editor.GetProviderRawField(name, "deploymentName") ?? string.Empty,
                    });
                }
            }
            catch
            {
                foreach (var (name, provider) in config.Providers.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    _providers.Add(new ProviderEntry
                    {
                        OriginalName = name,
                        Name = name,
                        Type = provider.Type,
                        ApiKey = provider.ApiKey ?? string.Empty,
                        Endpoint = provider.Endpoint ?? string.Empty,
                        Model = provider.Model ?? string.Empty,
                        DeploymentName = provider.DeploymentName ?? string.Empty,
                    });
                }
            }

            _presets.Clear();
            foreach (var (key, preset) in config.Presets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                _presets.Add(new PresetEntry
                {
                    OriginalKey = key,
                    Key = key,
                    Name = preset.Name,
                    SystemPrompt = preset.SystemPrompt,
                    Provider = preset.Provider ?? string.Empty,
                    IncludeInPicker = preset.IncludeInPicker,
                    PickerOrder = preset.PickerOrder ?? 0,
                });
            }

            _hotkeys.Clear();
            foreach (var hotkey in config.Hotkeys)
            {
                _hotkeys.Add(Clone(hotkey));
            }

            _pickerActions.Clear();
            foreach (var action in config.PickerActions)
            {
                _pickerActions.Add(Clone(action));
            }

            RebuildPickerRows();
            SetDirty(false);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ShowSelectedSection()
    {
        var section = _nav.SelectedItem as string ?? Sections[0];
        _sectionTitle.Text = section;
        _sectionSubtitle.Text = section switch
        {
            "General" => "Default provider, startup behavior, and custom prompt template.",
            "Providers" => "Configure AI backends and optional secret storage.",
            "Presets" => "Create reusable transformations and control picker visibility.",
            "Hotkeys" => "Bind global shortcuts to presets, prompt windows, or the picker.",
            "Operation picker" => "Order and curate the command palette entries.",
            "Notifications" => "Control status notifications and processing indicators.",
            "Advanced" => "Configuration paths, diagnostics, and reset tools.",
            _ => string.Empty,
        };

        _contentHost.Content = section switch
        {
            "General" => BuildGeneralPage(),
            "Providers" => BuildProvidersPage(),
            "Presets" => BuildPresetsPage(),
            "Hotkeys" => BuildHotkeysPage(),
            "Operation picker" => BuildPickerPage(),
            "Notifications" => BuildNotificationsPage(),
            "Advanced" => BuildAdvancedPage(),
            _ => BuildGeneralPage(),
        };
    }

    private AControl BuildGeneralPage()
    {
        var providerCombo = new AComboBox { ItemsSource = ProviderNames(), SelectedItem = _defaultProvider, MinWidth = 260 };
        providerCombo.SelectionChanged += (_, _) =>
        {
            _defaultProvider = providerCombo.SelectedItem as string ?? string.Empty;
            MarkDirty();
        };

        var startup = new ACheckBox { Content = "Run Turbophrase at Windows startup", IsChecked = _runAtStartup };
        startup.IsCheckedChanged += (_, _) =>
        {
            _runAtStartup = startup.IsChecked == true;
            MarkDirty();
        };

        var prompt = new ATextBox
        {
            Text = _customPromptTemplate,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 260,
            FontFamily = AFontFamily.Parse("Cascadia Mono, Consolas"),
            FontSize = 12,
        };
        prompt.TextChanged += (_, _) =>
        {
            _customPromptTemplate = prompt.Text ?? string.Empty;
            MarkDirty();
        };

        var reset = new AButton { Content = "Reset template" };
        reset.Click += (_, _) => prompt.Text = new CustomPromptSettings().SystemPromptTemplate;

        return CardStack(
            Field("Default provider", providerCombo, "Used when a preset does not specify a provider override."),
            Field("Windows startup", startup, StartupManager.GetStartupCommand() ?? "Adds a per-user startup entry."),
            Field("Custom prompt template", prompt, "Use {instruction} for the user prompt and {text} for the selected text."),
            reset);
    }

    private AControl BuildProvidersPage()
    {
        ProviderEntry? selected = _providers.FirstOrDefault();
        var list = new AListBox { ItemsSource = _providers, MinWidth = 230, MaxWidth = 260 };
        var detailHost = new ContentControl();

        void RenderDetail()
        {
            selected = list.SelectedItem as ProviderEntry;
            detailHost.Content = selected == null ? EmptyState("No provider selected.") : BuildProviderDetail(selected, list, detailHost);
        }

        list.SelectionChanged += (_, _) => RenderDetail();

        var add = new AButton { Classes = { "primary" }, Content = "Add provider" };
        add.Click += (_, _) =>
        {
            var entry = new ProviderEntry { Name = UniqueName("provider", _providers.Select(p => p.Name)), Type = "openai" };
            _providers.Add(entry);
            if (string.IsNullOrWhiteSpace(_defaultProvider))
            {
                _defaultProvider = entry.Name;
            }
            list.SelectedItem = entry;
            MarkDirty();
        };

        var remove = new AButton { Content = "Remove" };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is ProviderEntry entry)
            {
                _providers.Remove(entry);
                if (_defaultProvider == entry.Name)
                {
                    _defaultProvider = _providers.FirstOrDefault()?.Name ?? string.Empty;
                }
                MarkDirty();
            }
        };

        var left = new Border
        {
            Classes = { "softCard" },
            Padding = new AThickness(12),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    DockBottom(new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 8, Children = { add, remove } }),
                    list
                }
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*"), ColumnSpacing = 14 };
        grid.Children.Add(left);
        Grid.SetColumn(detailHost, 1);
        grid.Children.Add(detailHost);
        list.SelectedIndex = _providers.Count > 0 ? 0 : -1;
        RenderDetail();
        return grid;
    }

    private AControl BuildProviderDetail(ProviderEntry entry, AListBox list, ContentControl detailHost)
    {
        var name = Text(entry.Name, text =>
        {
            var previousName = entry.Name;
            entry.Name = text.Trim();
            RenameProviderReferences(previousName, entry.Name);
            MarkDirty();
        });

        var type = new AComboBox { ItemsSource = ProviderTypes, SelectedItem = entry.Type, MinWidth = 220 };
        type.SelectionChanged += (_, _) => { entry.Type = type.SelectedItem as string ?? string.Empty; MarkDirty(); };

        var apiKey = Text(entry.ApiKey, text => { entry.ApiKey = text; MarkDirty(); });
        var saveSecret = new ACheckBox { Content = "Save API key in Windows Credential Manager", IsChecked = entry.SaveApiKeyInCredMan };
        saveSecret.IsCheckedChanged += (_, _) => { entry.SaveApiKeyInCredMan = saveSecret.IsChecked == true; MarkDirty(); };

        var endpoint = Text(entry.Endpoint, text => { entry.Endpoint = text; MarkDirty(); });
        var model = Text(entry.Model, text => { entry.Model = text; MarkDirty(); });
        var deployment = Text(entry.DeploymentName, text => { entry.DeploymentName = text; MarkDirty(); });

        var setDefault = new AButton { Content = entry.Name == _defaultProvider ? "Default provider" : "Set as default" };
        setDefault.Click += (_, _) =>
        {
            _defaultProvider = entry.Name;
            setDefault.Content = "Default provider";
            MarkDirty();
        };

        var testResult = new TextBlock { Classes = { "muted" }, FontSize = 12 };
        var test = new AButton { Content = "Test connection" };
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            testResult.Text = "Testing...";
            var probe = new ProviderConfig
            {
                Type = entry.Type,
                ApiKey = ConfigurationService.ResolveSecretReference(entry.ApiKey),
                Endpoint = ConfigurationService.ResolveSecretReference(entry.Endpoint),
                Model = ConfigurationService.ResolveSecretReference(entry.Model),
                DeploymentName = ConfigurationService.ResolveSecretReference(entry.DeploymentName),
            };
            var result = await ProviderTester.TestAsync(entry.Name, probe);
            testResult.Foreground = result.Success ? Brush("TpSuccessBrush") : Brush("TpDangerBrush");
            testResult.Text = result.Success ? "Connection OK" : result.ErrorMessage ?? "Connection failed";
            test.IsEnabled = true;
        };

        return CardStack(
            SectionHeader(entry.Name, entry.Name == _defaultProvider ? "Currently used by default." : "Provider configuration."),
            Field("Name", name),
            Field("Type", type),
            Field("API key", apiKey),
            saveSecret,
            Field("Endpoint", endpoint),
            Field("Model", model),
            Field("Deployment name", deployment),
            new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 10, Children = { setDefault, test, testResult } });
    }

    private AControl BuildPresetsPage()
    {
        var list = new AListBox { ItemsSource = _presets, MinWidth = 230, MaxWidth = 260 };
        var detailHost = new ContentControl();

        void RenderDetail()
        {
            detailHost.Content = list.SelectedItem is PresetEntry entry ? BuildPresetDetail(entry, list) : EmptyState("No preset selected.");
        }
        list.SelectionChanged += (_, _) => RenderDetail();

        var add = new AButton { Classes = { "primary" }, Content = "Add preset" };
        add.Click += (_, _) =>
        {
            var entry = new PresetEntry
            {
                Key = UniqueName("preset", _presets.Select(p => p.Key)),
                Name = "New preset",
                SystemPrompt = "Describe how the AI should transform the selected text.",
                IncludeInPicker = true,
                PickerOrder = NextPickerOrder(),
            };
            _presets.Add(entry);
            list.SelectedItem = entry;
            RebuildPickerRows();
            MarkDirty();
        };

        var duplicate = new AButton { Content = "Duplicate" };
        duplicate.Click += (_, _) =>
        {
            if (list.SelectedItem is not PresetEntry source) return;
            var entry = new PresetEntry
            {
                Key = UniqueName(source.Key + "-copy", _presets.Select(p => p.Key)),
                Name = source.Name + " (copy)",
                SystemPrompt = source.SystemPrompt,
                Provider = source.Provider,
                IncludeInPicker = source.IncludeInPicker,
                PickerOrder = source.PickerOrder,
            };
            _presets.Add(entry);
            list.SelectedItem = entry;
            RebuildPickerRows();
            MarkDirty();
        };

        var remove = new AButton { Content = "Remove" };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is PresetEntry entry)
            {
                _presets.Remove(entry);
                RebuildPickerRows();
                MarkDirty();
            }
        };

        var left = new Border
        {
            Classes = { "softCard" },
            Padding = new AThickness(12),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    DockBottom(new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 8, Children = { add, duplicate, remove } }),
                    list
                }
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*"), ColumnSpacing = 14 };
        grid.Children.Add(left);
        Grid.SetColumn(detailHost, 1);
        grid.Children.Add(detailHost);
        list.SelectedIndex = _presets.Count > 0 ? 0 : -1;
        RenderDetail();
        return grid;
    }

    private AControl BuildPresetDetail(PresetEntry entry, AListBox list)
    {
        var key = Text(entry.Key, text =>
        {
            var previousKey = entry.Key;
            entry.Key = text.Trim();
            RenamePresetReferences(previousKey, entry.Key);
            RebuildPickerRows();
            MarkDirty();
        });
        var name = Text(entry.Name, text => { entry.Name = text; RebuildPickerRows(); MarkDirty(); });
        var prompt = Text(entry.SystemPrompt, text => { entry.SystemPrompt = text; MarkDirty(); }, multi: true, minHeight: 220);
        var provider = new AComboBox { ItemsSource = new[] { "" }.Concat(ProviderNames()).ToList(), SelectedItem = entry.Provider, MinWidth = 220 };
        provider.SelectionChanged += (_, _) => { entry.Provider = provider.SelectedItem as string ?? string.Empty; MarkDirty(); };
        var order = Text(entry.PickerOrder.ToString(), text => { if (int.TryParse(text, out var v)) entry.PickerOrder = v; MarkDirty(); });
        var include = new ACheckBox { Content = "Include in operation picker", IsChecked = entry.IncludeInPicker };
        include.IsCheckedChanged += (_, _) => { entry.IncludeInPicker = include.IsChecked == true; RebuildPickerRows(); MarkDirty(); };

        return CardStack(
            SectionHeader(entry.Name, "Reusable prompt transformation."),
            Field("Key", key),
            Field("Display name", name),
            Field("System prompt", prompt),
            Field("Provider override", provider, "Blank uses the default provider."),
            Field("Picker order", order),
            include);
    }

    private AControl BuildHotkeysPage()
    {
        var list = new AListBox { ItemsSource = _hotkeys, MinWidth = 260, MaxWidth = 300 };
        var detailHost = new ContentControl();
        void RenderDetail() => detailHost.Content = list.SelectedItem is HotkeyBinding h ? BuildHotkeyDetail(h) : EmptyState("No hotkey selected.");
        list.SelectionChanged += (_, _) => RenderDetail();

        var add = new AButton { Classes = { "primary" }, Content = "Add hotkey" };
        add.Click += (_, _) =>
        {
            var h = new HotkeyBinding { Keys = "Ctrl+Alt+T", Action = "preset", Preset = _presets.FirstOrDefault()?.Key ?? string.Empty };
            _hotkeys.Add(h);
            list.SelectedItem = h;
            RebuildPickerRows();
            MarkDirty();
        };
        var remove = new AButton { Content = "Remove" };
        remove.Click += (_, _) => { if (list.SelectedItem is HotkeyBinding h) { _hotkeys.Remove(h); RebuildPickerRows(); MarkDirty(); } };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*"), ColumnSpacing = 14 };
        grid.Children.Add(new Border
        {
            Classes = { "softCard" },
            Padding = new AThickness(12),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    DockBottom(new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 8, Children = { add, remove } }),
                    list
                }
            }
        });
        Grid.SetColumn(detailHost, 1);
        grid.Children.Add(detailHost);
        list.SelectedIndex = _hotkeys.Count > 0 ? 0 : -1;
        RenderDetail();
        return grid;
    }

    private AControl BuildHotkeyDetail(HotkeyBinding hotkey)
    {
        var keys = Text(hotkey.Keys, text => { hotkey.Keys = text; MarkDirty(); });
        var action = new AComboBox { ItemsSource = new[] { "preset", "custom-prompt", "preset-picker" }, SelectedItem = string.IsNullOrWhiteSpace(hotkey.Action) ? "preset" : hotkey.Action, MinWidth = 220 };
        action.SelectionChanged += (_, _) => { hotkey.Action = action.SelectedItem as string; RebuildPickerRows(); MarkDirty(); };
        var preset = new AComboBox { ItemsSource = _presets.Select(p => p.Key).ToList(), SelectedItem = hotkey.Preset, MinWidth = 220 };
        preset.SelectionChanged += (_, _) => { hotkey.Preset = preset.SelectedItem as string ?? string.Empty; MarkDirty(); };
        var name = Text(hotkey.Name ?? string.Empty, text => { hotkey.Name = string.IsNullOrWhiteSpace(text) ? null : text; RebuildPickerRows(); MarkDirty(); });
        var provider = new AComboBox { ItemsSource = new[] { "" }.Concat(ProviderNames()).ToList(), SelectedItem = hotkey.Provider ?? string.Empty, MinWidth = 220 };
        provider.SelectionChanged += (_, _) => { hotkey.Provider = string.IsNullOrWhiteSpace(provider.SelectedItem as string) ? null : provider.SelectedItem as string; MarkDirty(); };
        var template = Text(hotkey.SystemPromptTemplate ?? string.Empty, text => { hotkey.SystemPromptTemplate = string.IsNullOrWhiteSpace(text) ? null : text; MarkDirty(); }, multi: true, minHeight: 130);
        var include = new ACheckBox { Content = "Include this action in the operation picker", IsChecked = hotkey.IncludeInPicker };
        include.IsCheckedChanged += (_, _) => { hotkey.IncludeInPicker = include.IsChecked == true; RebuildPickerRows(); MarkDirty(); };
        var order = Text((hotkey.PickerOrder ?? 0).ToString(), text => { if (int.TryParse(text, out var v)) hotkey.PickerOrder = v == 0 ? null : v; MarkDirty(); });

        return CardStack(
            SectionHeader(hotkey.Keys, "Global shortcut binding."),
            Field("Keys", keys, "Example: Ctrl+Shift+G or Ctrl+F7."),
            Field("Action", action),
            Field("Preset", preset),
            Field("Display name", name),
            Field("Provider override", provider),
            Field("Prompt template", template),
            include,
            Field("Picker order", order));
    }

    private AControl BuildPickerPage()
    {
        RebuildPickerRows();
        var list = new AListBox { ItemsSource = _pickerRows, MinHeight = 360 };
        var include = new ACheckBox { Content = "Show selected item in picker" };
        void RefreshInclude()
        {
            include.IsEnabled = list.SelectedItem is PickerEntry;
            include.IsChecked = (list.SelectedItem as PickerEntry)?.IncludeInPicker;
        }
        list.SelectionChanged += (_, _) => RefreshInclude();
        include.IsCheckedChanged += (_, _) =>
        {
            if (list.SelectedItem is PickerEntry row)
            {
                row.IncludeInPicker = include.IsChecked == true;
                ApplyPickerRowsToModels();
                MarkDirty();
            }
        };

        var up = new AButton { Content = "Move up" };
        var down = new AButton { Content = "Move down" };
        up.Click += (_, _) => MovePickerRow(list, -1);
        down.Click += (_, _) => MovePickerRow(list, 1);

        var addAction = new AButton { Classes = { "primary" }, Content = "Add custom prompt action" };
        addAction.Click += (_, _) =>
        {
            var action = new HotkeyBinding { Action = "custom-prompt", Name = "New picker action", IncludeInPicker = true, PickerOrder = _pickerRows.Count + 1 };
            _pickerActions.Add(action);
            RebuildPickerRows();
            list.ItemsSource = null;
            list.ItemsSource = _pickerRows;
            list.SelectedItem = _pickerRows.LastOrDefault();
            MarkDirty();
        };

        var removeAction = new AButton { Content = "Remove picker-only action" };
        removeAction.Click += (_, _) =>
        {
            if (list.SelectedItem is PickerEntry { Source: "Picker action", Binding: { } binding })
            {
                _pickerActions.Remove(binding);
                RebuildPickerRows();
                list.ItemsSource = null;
                list.ItemsSource = _pickerRows;
                MarkDirty();
            }
        };

        list.SelectedIndex = _pickerRows.Count > 0 ? 0 : -1;
        RefreshInclude();
        return CardStack(
            new TextBlock { Classes = { "muted" }, Text = "Use this list to control what the command palette shows and in what order." },
            list,
            include,
            new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 10, Children = { up, down, addAction, removeAction } });
    }

    private AControl BuildNotificationsPage()
    {
        return CardStack(
            Check("Show notification on startup", _notifications.ShowOnStartup, v => _notifications.ShowOnStartup = v),
            Check("Show notification on successful transformation", _notifications.ShowOnSuccess, v => _notifications.ShowOnSuccess = v),
            Check("Show notification on errors", _notifications.ShowOnError, v => _notifications.ShowOnError = v),
            Check("Show notification on configuration reload", _notifications.ShowOnConfigReload, v => _notifications.ShowOnConfigReload = v),
            Check("Show notification on provider change", _notifications.ShowOnProviderChange, v => _notifications.ShowOnProviderChange = v),
            Check("Show processing overlay while transforming", _notifications.ShowProcessingOverlay, v => _notifications.ShowProcessingOverlay = v),
            Check("Animate tray icon while processing", _notifications.ShowProcessingAnimation, v => _notifications.ShowProcessingAnimation = v));
    }

    private AControl BuildAdvancedPage()
    {
        var logging = Check("Write diagnostic events to turbophrase.log", _logging.Enabled, v => _logging.Enabled = v);
        var openConfig = new AButton { Content = "Open config file" };
        openConfig.Click += (_, _) => SafeStart(ConfigurationService.ConfigFilePath, isFolder: false);
        var openFolder = new AButton { Content = "Open config folder" };
        openFolder.Click += (_, _) => SafeStart(ConfigurationService.ConfigDirectory, isFolder: true);
        var providerSetup = new AButton { Content = "Provider setup" };
        providerSetup.Click += async (_, _) =>
        {
            if (await FirstRunWindow.ShowProviderSetupAsync())
            {
                LoadConfiguration();
                ShowSelectedSection();
                _status.Foreground = Brush("TpSuccessBrush");
                _status.Text = "Provider setup saved.";
            }
        };
        var reset = new AButton { Content = "Reset to defaults" };
        reset.Click += (_, _) =>
        {
            ConfigEditor.ResetToDefaults(ConfigurationService.ConfigFilePath, createBackup: true);
            LoadConfiguration();
            ShowSelectedSection();
            _status.Foreground = Brush("TpSuccessBrush");
            _status.Text = "Configuration reset to defaults.";
        };

        return CardStack(
            ReadOnlyField("Config file", ConfigurationService.ConfigFilePath),
            ReadOnlyField("Config folder", ConfigurationService.ConfigDirectory),
            ReadOnlyField("Custom config", ConfigurationService.CustomConfigFilePath ?? "(not set)"),
            ReadOnlyField("XDG_CONFIG_HOME", Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "(not set)"),
            logging,
            new StackPanel { Orientation = AOrientation.Horizontal, Spacing = 10, Children = { openConfig, openFolder, providerSetup, reset } });
    }

    private void SaveAndMaybeClose(bool close)
    {
        if (!_dirty && close)
        {
            Close();
            return;
        }

        var validation = ValidateState();
        if (validation != null)
        {
            _status.Foreground = Brush("TpDangerBrush");
            _status.Text = validation;
            return;
        }

        try
        {
            var editor = ConfigEditor.LoadOrCreate(ConfigurationService.ConfigFilePath);
            editor.SetDefaultProvider(_defaultProvider);
            editor.SetCustomPromptTemplate(_customPromptTemplate);
            editor.SetNotifications(_notifications);
            editor.SetLogging(_logging);

            foreach (var provider in _providers)
            {
                if (string.IsNullOrEmpty(provider.OriginalName)
                    || string.Equals(provider.OriginalName, provider.Name, StringComparison.Ordinal)
                    || !editor.GetProviderNames().Contains(provider.OriginalName, StringComparer.Ordinal))
                {
                    continue;
                }

                var fields = ProviderFieldValues(provider).ToDictionary(pair => pair.Key, pair => pair.Value);
                editor.SetProviderFields(provider.OriginalName, fields.ToDictionary(pair => pair.Key, _ => (string?)null));
                editor.SetProviderFields(provider.Name, fields);
                provider.OriginalName = provider.Name;
            }

            var providerNames = _providers.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in editor.GetProviderNames().ToList())
            {
                if (!providerNames.Contains(existing))
                {
                    editor.RemoveProvider(existing);
                }
            }
            foreach (var provider in _providers)
            {
                editor.SetProviderFields(provider.Name, ProviderFieldValues(provider));
                provider.OriginalName = provider.Name;
            }

            foreach (var preset in _presets)
            {
                if (!string.IsNullOrEmpty(preset.OriginalKey)
                    && !string.Equals(preset.OriginalKey, preset.Key, StringComparison.Ordinal)
                    && editor.RenamePreset(preset.OriginalKey, preset.Key))
                {
                    preset.OriginalKey = preset.Key;
                }
            }

            var presetKeys = _presets.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in editor.GetPresetNames().ToList())
            {
                if (!presetKeys.Contains(existing))
                {
                    editor.RemovePreset(existing);
                }
            }
            foreach (var preset in _presets)
            {
                editor.SetPreset(preset.Key, new PromptPreset
                {
                    Name = preset.Name,
                    SystemPrompt = preset.SystemPrompt,
                    Provider = Normalize(preset.Provider),
                    IncludeInPicker = preset.IncludeInPicker,
                    PickerOrder = preset.PickerOrder == 0 ? null : preset.PickerOrder,
                });
                preset.OriginalKey = preset.Key;
            }

            editor.SetHotkeys(_hotkeys);
            editor.SetPickerActions(_pickerActions);
            editor.Save();

            if (_runAtStartup)
            {
                StartupManager.Enable(ConfigurationService.CustomConfigFilePath);
            }
            else
            {
                StartupManager.Disable();
            }

            SetDirty(false);
            _status.Foreground = Brush("TpSuccessBrush");
            _status.Text = $"Saved {DateTime.Now:HH:mm:ss}";
            if (close)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            _status.Foreground = Brush("TpDangerBrush");
            _status.Text = ex.Message;
        }
    }

    private void RenameProviderReferences(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)
            || string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(_defaultProvider, oldName, StringComparison.OrdinalIgnoreCase))
        {
            _defaultProvider = newName;
        }

        foreach (var preset in _presets.Where(p => string.Equals(p.Provider, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            preset.Provider = newName;
        }

        foreach (var binding in _hotkeys.Concat(_pickerActions).Where(h => string.Equals(h.Provider, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            binding.Provider = newName;
        }
    }

    private void RenamePresetReferences(string oldKey, string newKey)
    {
        if (string.IsNullOrWhiteSpace(oldKey) || string.IsNullOrWhiteSpace(newKey)
            || string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var binding in _hotkeys.Concat(_pickerActions).Where(h => string.Equals(h.Preset, oldKey, StringComparison.OrdinalIgnoreCase)))
        {
            binding.Preset = newKey;
        }
    }

    private string? ValidateState()
    {
        if (string.IsNullOrWhiteSpace(_defaultProvider)) return "Choose a default provider.";
        if (string.IsNullOrWhiteSpace(_customPromptTemplate)) return "Custom prompt template cannot be empty.";
        if (_providers.Any(p => string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Type))) return "Every provider needs a name and type.";
        if (_providers.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) return "Provider names must be unique.";
        foreach (var provider in _providers)
        {
            if (RequiresApiKey(provider.Type) && IsUnresolvedOrBlank(provider.ApiKey)) return $"Provider '{provider.Name}' needs a resolved API key.";
            if (provider.Type == "azure-openai" && IsUnresolvedOrBlank(provider.Endpoint)) return $"Provider '{provider.Name}' needs a resolved Azure endpoint.";
            if (provider.Type == "azure-openai" && IsUnresolvedOrBlank(provider.DeploymentName) && IsUnresolvedOrBlank(provider.Model)) return $"Provider '{provider.Name}' needs an Azure deployment name.";
        }
        if (_presets.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.SystemPrompt))) return "Every preset needs a key, name, and prompt.";
        if (_presets.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) return "Preset keys must be unique.";
        if (_hotkeys.Any(h => string.IsNullOrWhiteSpace(h.Keys))) return "Every hotkey needs a key combination.";
        return null;
    }

    private string? ResolveApiKeyForSave(ProviderEntry entry)
    {
        var value = Normalize(entry.ApiKey);
        if (value == null) return null;
        if (!entry.SaveApiKeyInCredMan || value.StartsWith(ConfigurationService.CredManPrefix, StringComparison.Ordinal) || value.StartsWith("${", StringComparison.Ordinal))
        {
            return value;
        }
        var name = $"{entry.Name}:apiKey";
        new SecretsStore().Save(name, value);
        return ConfigurationService.CredManPrefix + name;
    }

    private static bool RequiresApiKey(string type) => type is "openai" or "azure-openai" or "anthropic";

    private static bool IsUnresolvedOrBlank(string? value) => string.IsNullOrWhiteSpace(value)
        || value.TrimStart().StartsWith("${", StringComparison.Ordinal);

    private IDictionary<string, string?> ProviderFieldValues(ProviderEntry provider) => new Dictionary<string, string?>
    {
        ["type"] = provider.Type,
        ["apiKey"] = ResolveApiKeyForSave(provider),
        ["endpoint"] = Normalize(provider.Endpoint),
        ["model"] = Normalize(provider.Model),
        ["deploymentName"] = Normalize(provider.DeploymentName),
    };

    private void RebuildPickerRows()
    {
        _pickerRows.Clear();
        foreach (var preset in _presets)
        {
            _pickerRows.Add(new PickerEntry("Preset", preset.Key, preset.Name, preset.IncludeInPicker, preset.PickerOrder, preset, null));
        }
        foreach (var hotkey in _hotkeys.Where(binding => !binding.IsPresetAction))
        {
            _pickerRows.Add(new PickerEntry("Hotkey", hotkey.Keys, DescribeHotkey(hotkey), hotkey.IncludeInPicker, hotkey.PickerOrder ?? 0, null, hotkey));
        }
        foreach (var action in _pickerActions)
        {
            _pickerRows.Add(new PickerEntry("Picker action", action.Name ?? action.Action ?? "Action", DescribeHotkey(action), action.IncludeInPicker, action.PickerOrder ?? 0, null, action));
        }
        SortPickerRows();
    }

    private void SortPickerRows()
    {
        var sorted = _pickerRows
            .Select((row, index) => (row, index))
            .OrderBy(item => item.row.Order == 0 ? int.MaxValue : item.row.Order)
            .ThenBy(item => item.index)
            .Select(item => item.row)
            .ToList();
        _pickerRows.Clear();
        foreach (var row in sorted)
        {
            _pickerRows.Add(row);
        }
    }

    private void MovePickerRow(AListBox list, int delta)
    {
        if (list.SelectedItem is not PickerEntry row) return;
        var index = _pickerRows.IndexOf(row);
        var next = Math.Clamp(index + delta, 0, _pickerRows.Count - 1);
        if (next == index) return;
        _pickerRows.Move(index, next);
        ApplyPickerRowsToModels();
        list.SelectedItem = row;
        MarkDirty();
    }

    private void ApplyPickerRowsToModels()
    {
        for (var i = 0; i < _pickerRows.Count; i++)
        {
            var row = _pickerRows[i];
            row.Order = i + 1;
            if (row.Preset != null)
            {
                row.Preset.IncludeInPicker = row.IncludeInPicker;
                row.Preset.PickerOrder = row.Order;
            }
            if (row.Binding != null)
            {
                row.Binding.IncludeInPicker = row.IncludeInPicker;
                row.Binding.PickerOrder = row.Order;
            }
        }
    }

    private static string DescribeHotkey(HotkeyBinding h)
    {
        if (h.IsCustomPromptAction) return string.IsNullOrWhiteSpace(h.Name) ? "Custom prompt" : h.Name!;
        if (h.IsPresetPickerAction) return string.IsNullOrWhiteSpace(h.Name) ? "Choose operation" : h.Name!;
        return h.Preset;
    }

    private IEnumerable<string> ProviderNames() => _providers.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

    private int NextPickerOrder() => Math.Max(0, _presets.Count == 0 ? 0 : _presets.Max(p => p.PickerOrder)) + 1;

    private void MarkDirty()
    {
        if (!_loading)
        {
            SetDirty(true);
        }
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        _applyButton.IsEnabled = dirty;
        _saveButton.Content = dirty ? "Save" : "Close";
        if (dirty)
        {
            _status.Foreground = Brush("TpMutedTextBrush");
            _status.Text = "Unsaved changes";
        }
        else if (string.IsNullOrWhiteSpace(_status.Text))
        {
            _status.Text = string.Empty;
        }
    }

    private StackPanel CardStack(params AControl[] children)
    {
        var panel = new StackPanel { Spacing = 14 };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Border
                {
                    Classes = { "card" },
                    Padding = new AThickness(18),
                    Child = panel
                }
            }
        };
    }

    private static AControl SectionHeader(string title, string subtitle) => new StackPanel
    {
        Spacing = 3,
        Children =
        {
            new TextBlock { Classes = { "sectionTitle" }, Text = title },
            new TextBlock { Classes = { "muted" }, Text = subtitle, FontSize = 12 }
        }
    };

    private static AControl Field(string label, AControl control, string? hint = null) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("170,*"),
        ColumnSpacing = 14,
        Children =
        {
            new TextBlock { Classes = { "muted" }, Text = label, VerticalAlignment = VerticalAlignment.Center },
            WithColumn(new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    control,
                    hint == null ? new TextBlock { IsVisible = false } : new TextBlock { Classes = { "subtle" }, Text = hint, FontSize = 11 }
                }
            }, 1)
        }
    };

    private static ATextBox Text(string value, Action<string> changed, bool multi = false, double minHeight = 0)
    {
        var box = new ATextBox
        {
            Text = value,
            AcceptsReturn = multi,
            TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = minHeight,
        };
        box.TextChanged += (_, _) => changed(box.Text ?? string.Empty);
        return box;
    }

    private AControl Check(string label, bool value, Action<bool> changed)
    {
        var box = new ACheckBox { Content = label, IsChecked = value };
        box.IsCheckedChanged += (_, _) => { changed(box.IsChecked == true); MarkDirty(); };
        return box;
    }

    private static AControl ReadOnlyField(string label, string value) => Field(label, new ATextBox { Text = value, IsReadOnly = true });

    private static AControl EmptyState(string text) => new Border
    {
        Classes = { "softCard" },
        Padding = new AThickness(18),
        Child = new TextBlock { Classes = { "muted" }, Text = text }
    };

    private static T WithColumn<T>(T control, int column) where T : AControl
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static AControl DockBottom(AControl control)
    {
        DockPanel.SetDock(control, Dock.Bottom);
        return control;
    }

    private static string UniqueName(string baseName, IEnumerable<string> existing)
    {
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = baseName;
        var i = 2;
        while (set.Contains(name))
        {
            name = baseName + i;
            i++;
        }
        return name;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static HotkeyBinding Clone(HotkeyBinding h) => new()
    {
        Keys = h.Keys,
        Action = h.Action,
        Preset = h.Preset,
        Name = h.Name,
        SystemPromptTemplate = h.SystemPromptTemplate,
        Provider = h.Provider,
        IncludeInPicker = h.IncludeInPicker,
        PickerOrder = h.PickerOrder,
    };

    private static void SafeStart(string path, bool isFolder)
    {
        if (isFolder && !Directory.Exists(path)) Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = isFolder ? "explorer.exe" : path, Arguments = isFolder ? $"\"{path}\"" : string.Empty, UseShellExecute = true });
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class ProviderEntry
    {
        public string OriginalName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public bool SaveApiKeyInCredMan { get; set; }
        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "(unnamed provider)" : Name;
    }

    private sealed class PresetEntry
    {
        public string OriginalKey { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IncludeInPicker { get; set; } = true;
        public int PickerOrder { get; set; }
        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Key : Name;
    }

    private sealed class PickerEntry(string source, string key, string display, bool includeInPicker, int order, PresetEntry? preset, HotkeyBinding? binding)
    {
        public string Source { get; } = source;
        public string Key { get; } = key;
        public string Display { get; } = display;
        public bool IncludeInPicker { get; set; } = includeInPicker;
        public int Order { get; set; } = order;
        public PresetEntry? Preset { get; } = preset;
        public HotkeyBinding? Binding { get; } = binding;
        public override string ToString() => $"{Display}  ({Source})";
    }
}
