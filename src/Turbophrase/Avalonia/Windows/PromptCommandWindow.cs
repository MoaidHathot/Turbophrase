using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AApplication = Avalonia.Application;
using AButton = Avalonia.Controls.Button;
using ABrushes = Avalonia.Media.Brushes;
using AColor = Avalonia.Media.Color;
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
    private readonly TextBlock _statusText;
    private readonly AButton _runButton;
    private bool _activateWhenOpened;

    public PromptCommandWindow(IEnumerable<string> providers, string defaultProvider)
    {
        Title = "Custom Prompt";
        Width = 700;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ExtendClientAreaToDecorationsHint = false;
        WindowDecorations = WindowDecorations.Full;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        Background = new SolidColorBrush(AColor.FromRgb(10, 14, 24));

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

        _providerBox = new AComboBox
        {
            MinWidth = 220,
            ItemsSource = providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
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

        Content = BuildContent();
        Opened += (_, _) =>
        {
            if (_activateWhenOpened)
            {
                Dispatcher.UIThread.Post(ActivateAndFocus, DispatcherPriority.Input);
            }
        };
        KeyDown += OnKeyDown;
    }

    public string PromptText { get; private set; } = string.Empty;

    public string? SelectedProvider { get; private set; }

    public bool Accepted { get; private set; }

    public void SetCapturePending()
    {
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Capturing selected text...";
    }

    public void SetCaptureReady()
    {
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Ctrl+Enter runs. Esc cancels.";
    }

    public void SetCaptureFailed(string message)
    {
        _statusText.Foreground = Brush("TpDangerBrush");
        _statusText.Text = message;
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
            Background = new SolidColorBrush(AColor.FromRgb(10, 14, 24)),
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
        titleStack.Children.Add(new TextBlock { Text = "Custom prompt", FontSize = 26, FontWeight = FontWeight.SemiBold, LineHeight = 30 });
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
        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Submit();
            e.Handled = true;
        }
    }

    private void OnPromptBoxKeyDown(object? sender, AKeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Submit();
            e.Handled = true;
        }
    }

    private void Submit()
    {
        PromptText = (_promptBox.Text ?? string.Empty).Trim();
        SelectedProvider = _providerBox.SelectedItem as string;
        Accepted = true;
        Close();
    }

    private void Cancel() => Close();

    private static IBrush Brush(string key) =>
        AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
