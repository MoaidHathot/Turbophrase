using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Turbophrase.Services;
using AApplication = Avalonia.Application;
using AButton = Avalonia.Controls.Button;
using ABrushes = Avalonia.Media.Brushes;
using AColor = Avalonia.Media.Color;
using AControl = Avalonia.Controls.Control;
using ACornerRadius = Avalonia.CornerRadius;
using ACursor = Avalonia.Input.Cursor;
using ATextBox = Avalonia.Controls.TextBox;
using AThickness = Avalonia.Thickness;
using AKeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Turbophrase.Avalonia.Windows;

public sealed class CommandPaletteWindow : Window
{
    private const int VisibleRowsBeforeScroll = 10;
    private const double RowHeight = 32;
    private const double RowSpacing = 4;

    private readonly ATextBox _filterBox;
    private readonly StackPanel _itemsPanel;
    private readonly TextBlock _statusText;
    private readonly IReadOnlyList<PickerOperation> _allOperations;
    private readonly List<PickerOperation> _visibleOperations = new();
    private bool _captureReady;
    private int _selectedIndex;
    private bool _activateWhenOpened;

    public CommandPaletteWindow(IEnumerable<PickerOperation> operations)
    {
        _allOperations = operations
            .Select((operation, index) => operation with { Number = index + 1 })
            .ToList();

        Title = "Choose Operation";
        RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark;
        Width = 680;
        SizeToContent = SizeToContent.Height;
        MinHeight = 360;
        MaxHeight = Math.Min(900, Screens.Primary?.WorkingArea.Height - 120 ?? 900);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ExtendClientAreaToDecorationsHint = false;
        WindowDecorations = WindowDecorations.Full;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.Mica, WindowTransparencyLevel.None];
        Background = ABrushes.Transparent;

        _filterBox = new ATextBox
        {
            PlaceholderText = "Search actions or type a row number...",
            FontSize = 14,
            MinHeight = 38,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();

        _itemsPanel = new StackPanel { Spacing = RowSpacing };
        _statusText = new TextBlock
        {
            Classes = { "muted" },
            Text = "Capturing selected text...",
            FontSize = 12
        };

        Content = BuildContent();
        Opened += (_, _) =>
        {
            if (_activateWhenOpened)
            {
                Dispatcher.UIThread.Post(ActivateAndFocus, DispatcherPriority.Input);
            }
        };
        KeyDown += OnKeyDown;
        ApplyFilter();
    }

    public bool Accepted { get; private set; }

    public PickerOperation? AcceptedOperation { get; private set; }

    public void SetCapturePending()
    {
        _captureReady = false;
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Capturing selected text...";
    }

    public void SetCaptureReady()
    {
        _captureReady = true;
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Text captured. Type to filter, Enter selects, Esc cancels.";
    }

    public void SetCaptureFailed(string message)
    {
        _captureReady = false;
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
        FocusFilterBox();
    }

    private void FocusFilterBox()
    {
        _filterBox.Focus();
        _filterBox.SelectAll();
    }

    private AControl BuildContent()
    {
        var root = new Border
        {
            CornerRadius = new ACornerRadius(0),
            Background = Brush("TpAcrylicTintBrush"),
            BorderThickness = new AThickness(0),
            Padding = new AThickness(18)
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 10
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock { Text = "Command palette", FontSize = 22, FontWeight = FontWeight.SemiBold, LineHeight = 27 });
        titleStack.Children.Add(new TextBlock { Classes = { "muted" }, Text = "Type, choose, transform. No detours.", FontSize = 13 });

        var closeButton = new AButton { Classes = { "ghost" }, Content = "Esc", Padding = new AThickness(10, 6) };
        closeButton.Click += (_, _) => Cancel();

        header.Children.Add(titleStack);
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(closeButton);

        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid.SetRow(_filterBox, 1);
        layout.Children.Add(_filterBox);

        var scroller = new ScrollViewer
        {
            Content = _itemsPanel,
            MaxHeight = GetListHeight(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scroller, 2);
        layout.Children.Add(scroller);

        Grid.SetRow(_statusText, 3);
        layout.Children.Add(_statusText);

        root.Child = layout;
        return root;
    }

    private double GetListHeight()
    {
        var visibleRows = Math.Min(_allOperations.Count, VisibleRowsBeforeScroll);
        var desired = visibleRows * RowHeight + Math.Max(0, visibleRows - 1) * RowSpacing;
        var available = (Screens.Primary?.WorkingArea.Height ?? 900) - 210;
        return Math.Min(desired, Math.Max(RowHeight, available));
    }

    private void ApplyFilter()
    {
        var filter = (_filterBox.Text ?? string.Empty).Trim();
        var matches = string.IsNullOrWhiteSpace(filter)
            ? _allOperations.ToList()
            : _allOperations
                .Where(operation => MatchesFilter(operation, filter))
                .ToList();

        _visibleOperations.Clear();
        _visibleOperations.AddRange(matches);
        _selectedIndex = _visibleOperations.Count > 0 ? 0 : -1;
        RebuildRows();
    }

    private void RebuildRows()
    {
        _itemsPanel.Children.Clear();

        for (var index = 0; index < _visibleOperations.Count; index++)
        {
            _itemsPanel.Children.Add(CreateRow(_visibleOperations[index], index, selected: index == _selectedIndex));
        }

        if (_visibleOperations.Count == 0)
        {
            _itemsPanel.Children.Add(new Border
            {
                Classes = { "softCard" },
                Padding = new AThickness(16),
                Child = new TextBlock
                {
                    Classes = { "muted" },
                    Text = "No matching operation."
                }
            });
        }
    }

    private AControl CreateRow(PickerOperation operation, int index, bool selected)
    {
        var border = new Border
        {
            Height = RowHeight,
            CornerRadius = new ACornerRadius(6),
            BorderThickness = selected ? new AThickness(1) : new AThickness(0),
            BorderBrush = selected ? Brush("TpAccentBrush") : ABrushes.Transparent,
            Background = selected ? Brush("TpAccentSoftBrush") : ABrushes.Transparent,
            Padding = new AThickness(8, 0),
            Cursor = new ACursor(StandardCursorType.Hand)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("26,*"),
            ColumnSpacing = 8
        };

        var number = new Border
        {
            Width = 26,
            Height = 22,
            CornerRadius = new ACornerRadius(4),
            BorderBrush = selected ? Brush("TpAccentBrush") : Brush("TpStrokeStrongBrush"),
            BorderThickness = new AThickness(1),
            Background = selected ? Brush("TpAccentBrush") : NeutralKeyBrush(),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = operation.Number.ToString(),
                FontWeight = FontWeight.SemiBold,
                FontSize = 11,
                Foreground = selected ? Brush("TpAccentTextBrush") : NeutralKeyTextBrush(),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(number);

        var name = new TextBlock
        {
            Text = operation.DisplayName,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        border.Child = grid;
        border.PointerPressed += (_, _) =>
        {
            _selectedIndex = index;
            RebuildRows();
        };
        border.DoubleTapped += (_, _) => SubmitSelectedItem();
        return border;
    }

    private void OnKeyDown(object? sender, AKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Cancel();
                e.Handled = true;
                break;
            case Key.Enter:
                SubmitSelectedItem();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_visibleOperations.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _visibleOperations.Count - 1);
        RebuildRows();
    }

    private void SubmitSelectedItem()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _visibleOperations.Count)
        {
            return;
        }

        if (!_captureReady)
        {
            _statusText.Foreground = Brush("TpDangerBrush");
            _statusText.Text = "Text capture must succeed before an operation can run.";
            return;
        }

        AcceptedOperation = _visibleOperations[_selectedIndex];
        Accepted = true;
        Close();
    }

    private void Cancel() => Close();

    private static bool MatchesFilter(PickerOperation operation, string filter)
    {
        if (operation.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || operation.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter.All(char.IsDigit)
            && operation.Number.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private IBrush Brush(string key) =>
        TryGetResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;

    private IBrush NeutralKeyBrush() => ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Light
        ? new SolidColorBrush(AColor.FromArgb(0xE6, 0xFF, 0xFF, 0xFF))
        : new SolidColorBrush(AColor.FromArgb(0xF2, 0x2D, 0x2D, 0x2D));

    private IBrush NeutralKeyTextBrush() => ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Light
        ? new SolidColorBrush(AColor.FromRgb(0x1A, 0x1A, 0x1A))
        : new SolidColorBrush(AColor.FromRgb(0xFF, 0xFF, 0xFF));
}
