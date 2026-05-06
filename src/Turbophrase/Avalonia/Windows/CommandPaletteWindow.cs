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
    private readonly ATextBox _filterBox;
    private readonly StackPanel _itemsPanel;
    private readonly TextBlock _statusText;
    private readonly IReadOnlyList<PickerOperation> _allOperations;
    private readonly List<PickerOperation> _visibleOperations = new();
    private int _selectedIndex;
    private bool _activateWhenOpened;

    public CommandPaletteWindow(IEnumerable<PickerOperation> operations)
    {
        _allOperations = operations
            .Select((operation, index) => operation with { Number = index + 1 })
            .ToList();

        Title = "Choose Operation";
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
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        Background = new SolidColorBrush(AColor.FromRgb(10, 14, 24));

        _filterBox = new ATextBox
        {
            PlaceholderText = "Search actions or type a row number...",
            FontSize = 15,
            MinHeight = 48,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();

        _itemsPanel = new StackPanel { Spacing = 8 };
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
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Capturing selected text...";
    }

    public void SetCaptureReady()
    {
        _statusText.Foreground = Brush("TpMutedTextBrush");
        _statusText.Text = "Text captured. Type to filter, Enter selects, Esc cancels.";
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
            Background = new SolidColorBrush(AColor.FromRgb(10, 14, 24)),
            BorderThickness = new AThickness(0),
            Padding = new AThickness(24)
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 14
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock { Text = "Command palette", FontSize = 26, FontWeight = FontWeight.SemiBold, LineHeight = 30 });
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
        var desired = _allOperations.Count * 63 + 12;
        var available = (Screens.Primary?.WorkingArea.Height ?? 900) - 240;
        return Math.Clamp(desired, 260, Math.Max(260, available));
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
            CornerRadius = new ACornerRadius(18),
            BorderThickness = new AThickness(1),
            BorderBrush = selected ? Brush("TpAccent2Brush") : Brush("TpStrokeBrush"),
            Background = selected ? new SolidColorBrush(AColor.FromRgb(24, 43, 72)) : Brush("TpSurfaceRaisedBrush"),
            Padding = new AThickness(15, 11),
            Cursor = new ACursor(StandardCursorType.Hand)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("42,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8
        };

        var number = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new ACornerRadius(10),
            BorderBrush = selected ? ABrushes.Transparent : Brush("TpStrokeStrongBrush"),
            BorderThickness = new AThickness(1),
            Background = selected
                ? new SolidColorBrush(AColor.FromRgb(165, 243, 252))
                : new SolidColorBrush(AColor.FromRgb(17, 27, 45)),
            Child = new TextBlock
            {
                Text = operation.Number.ToString(),
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Foreground = selected
                    ? new SolidColorBrush(AColor.FromRgb(3, 13, 24))
                    : Brush("TpMutedTextBrush"),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRowSpan(number, 2);
        grid.Children.Add(number);

        var name = new TextBlock
        {
            Text = operation.DisplayName,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var id = new TextBlock
        {
            Classes = { "muted" },
            Text = operation.Id,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(id, 1);
        Grid.SetRow(id, 1);
        grid.Children.Add(id);

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

    private static IBrush Brush(string key) =>
        AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
