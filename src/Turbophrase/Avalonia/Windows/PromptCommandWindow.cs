using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AApplication = Avalonia.Application;
using AButton = Avalonia.Controls.Button;
using ABrushes = Avalonia.Media.Brushes;
using AComboBox = Avalonia.Controls.ComboBox;
using AControl = Avalonia.Controls.Control;
using ATextBox = Avalonia.Controls.TextBox;
using AThickness = Avalonia.Thickness;
using AKeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Turbophrase.Avalonia.Windows;

public sealed class PromptCommandWindow : Window
{
    private readonly ATextBox _promptBox;
    private readonly AComboBox _providerBox;
    private readonly List<string> _providers;
    private readonly TextBlock _statusText;
    private readonly AButton _runButton;
    private bool _activateWhenOpened;
    private bool _captureReady;

    public PromptCommandWindow(IEnumerable<string> providers, string defaultProvider)
    {
        _providers = providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        Title = "Custom Prompt";
        Width = 700;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ExtendClientAreaToDecorationsHint = false;
        WindowDecorations = WindowDecorations.Full;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];
        Background = ABrushes.Transparent;

        _promptBox = new ATextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 176,
            MaxHeight = 250,
            FontSize = 14,
            PlaceholderText = "Rewrite this as a concise update, translate it, make it friendlier..."
        };
        _promptBox.KeyDown += OnPromptBoxKeyDown;
        _promptBox.TextChanged += (_, _) => UpdateRunState();

        _providerBox = new AComboBox
        {
            MinWidth = 220,
            ItemsSource = _providers
        };
        _providerBox.SelectedItem = _providerBox.Items.Cast<string?>().FirstOrDefault(item => item == defaultProvider)
            ?? _providerBox.Items.Cast<string?>().FirstOrDefault();

        _statusText = new TextBlock
        {
            Classes = { "muted" },
            Text = "Capturing selected text...",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        _runButton = new AButton
        {
            Classes = { "primary" },
            Content = "Run",
            MinWidth = 96
        };
        _runButton.Click += (_, _) => Submit();
        UpdateRunState();

        Content = BuildContent();
        Opened += (_, _) =>
        {
            if (_activateWhenOpened)
            {
                Dispatcher.UIThread.Post(ActivateAndFocus, DispatcherPriority.Input);
            }
        };
        KeyDown += OnKeyDown;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public string PromptText { get; private set; } = string.Empty;

    public string? SelectedProvider { get; private set; }

    public bool Accepted { get; private set; }

    public void SetCapturePending()
    {
        _captureReady = false;
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Capturing selected text...";
        UpdateRunState();
    }

    public void SetCaptureReady()
    {
        _captureReady = true;
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Ctrl+Enter runs. Ctrl+Up/Down changes provider. Alt+1-9 jumps provider. Esc cancels.";
        UpdateRunState();
    }

    public void SetCaptureFailed(string message)
    {
        _captureReady = false;
        _statusText.Foreground = Brush("TpDangerBrush");
        _statusText.Text = message;
        UpdateRunState();
    }

    public void ActivateForInput()
    {
        _activateWhenOpened = true;
        if (!IsVisible)
        {
            return;
        }

        ActivateAndFocus();
        Dispatcher.UIThread.Post(ActivateAndFocus, DispatcherPriority.Input);
    }

    private void ActivateAndFocus()
    {
        ShowActivated = true;
        Topmost = false;
        Topmost = true;
        Activate();
        Focus();
        FocusPromptBox();
    }

    private void FocusPromptBox()
    {
        _promptBox.Focus();
    }

    private AControl BuildContent()
    {
        var root = new Border
        {
            CornerRadius = new CornerRadius(0),
            Background = Brush("TpAppBackground"),
            BorderThickness = new AThickness(0),
            Padding = new AThickness(24)
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 16
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock { Text = "Custom prompt", FontSize = 24, FontWeight = FontWeight.SemiBold, LineHeight = 29 });
        titleStack.Children.Add(new TextBlock { Classes = { "muted" }, Text = "Describe the transformation. Turbophrase handles the selected text.", FontSize = 13 });

        var closeButton = new AButton { Classes = { "ghost" }, Content = "Esc", Padding = new AThickness(10, 6) };
        closeButton.Click += (_, _) => Cancel();

        header.Children.Add(titleStack);
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(closeButton);
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid.SetRow(_promptBox, 1);
        layout.Children.Add(_promptBox);

        var providerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 12
        };
        providerRow.Children.Add(new TextBlock
        {
            Classes = { "muted" },
            Text = "Provider",
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(_providerBox, 1);
        providerRow.Children.Add(_providerBox);
        Grid.SetColumn(_runButton, 2);
        providerRow.Children.Add(_runButton);
        Grid.SetRow(providerRow, 2);
        layout.Children.Add(providerRow);

        Grid.SetRow(_statusText, 3);
        layout.Children.Add(_statusText);

        root.Child = layout;
        return root;
    }

    private void OnKeyDown(object? sender, AKeyEventArgs e)
    {
        HandleShortcut(e);
    }

    private void OnPromptBoxKeyDown(object? sender, AKeyEventArgs e)
    {
        HandleShortcut(e);
    }

    private void OnPreviewKeyDown(object? sender, AKeyEventArgs e)
    {
        HandleShortcut(e);
    }

    private void HandleShortcut(AKeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_runButton.IsEnabled)
            {
                Submit();
            }
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.Up or Key.Down)
        {
            MoveProvider(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        var providerNumber = GetProviderNumber(e.Key);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && providerNumber > 0)
        {
            SelectProvider(providerNumber - 1);
            e.Handled = true;
        }
    }

    private void MoveProvider(int delta)
    {
        if (_providers.Count == 0)
        {
            return;
        }

        var current = _providers.IndexOf(_providerBox.SelectedItem as string ?? string.Empty);
        if (current < 0)
        {
            current = 0;
        }

        SelectProvider((current + delta + _providers.Count) % _providers.Count);
    }

    private void SelectProvider(int index)
    {
        if (index >= 0 && index < _providers.Count)
        {
            _providerBox.SelectedItem = _providers[index];
        }
    }

    private static int GetProviderNumber(Key key) => key switch
    {
        Key.D1 or Key.NumPad1 => 1,
        Key.D2 or Key.NumPad2 => 2,
        Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4,
        Key.D5 or Key.NumPad5 => 5,
        Key.D6 or Key.NumPad6 => 6,
        Key.D7 or Key.NumPad7 => 7,
        Key.D8 or Key.NumPad8 => 8,
        Key.D9 or Key.NumPad9 => 9,
        _ => 0,
    };

    private void Submit()
    {
        PromptText = (_promptBox.Text ?? string.Empty).Trim();
        if (!_captureReady || string.IsNullOrWhiteSpace(PromptText))
        {
            _statusText.Foreground = Brush("TpDangerBrush");
            _statusText.Text = !_captureReady
                ? "Text capture must succeed before the prompt can run."
                : "Prompt cannot be empty.";
            UpdateRunState();
            return;
        }

        SelectedProvider = _providerBox.SelectedItem as string;
        Accepted = true;
        Close();
    }

    private void UpdateRunState()
    {
        _runButton.IsEnabled = _captureReady && !string.IsNullOrWhiteSpace(_promptBox.Text);
    }

    private void Cancel() => Close();

    private static IBrush Brush(string key) =>
        AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
