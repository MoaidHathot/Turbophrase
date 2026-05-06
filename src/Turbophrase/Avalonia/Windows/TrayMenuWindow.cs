using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AApplication = Avalonia.Application;
using ABrushes = Avalonia.Media.Brushes;
using AColor = Avalonia.Media.Color;
using AControl = Avalonia.Controls.Control;
using ACursor = Avalonia.Input.Cursor;
using AThickness = Avalonia.Thickness;

namespace Turbophrase.Avalonia.Windows;

public sealed class TrayMenuWindow : Window
{
    private readonly IReadOnlyList<TrayMenuSection> _sections;

    public TrayMenuWindow(IEnumerable<TrayMenuSection> sections)
    {
        _sections = sections.ToList();
        Title = "Turbophrase";
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        Background = AApplication.Current?.FindResource("TpAppBackground") as IBrush ?? Brush("TpVoidBrush");

        var screen = DisplayPlacement.GetScreenNearCursor(Screens) ?? Screens.Primary;
        var scale = screen?.Scaling is > 0 ? screen.Scaling : 1;
        var area = screen?.WorkingArea;
        Width = Math.Clamp((area?.Width / scale ?? 420) * 0.26, 320, 400);
        MaxHeight = Math.Max(420, (area?.Height / scale ?? 760) - 96);

        Content = BuildContent();
        Opened += (_, _) => Dispatcher.UIThread.Post(PositionNearCursor, DispatcherPriority.Loaded);
        Deactivated += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private AControl BuildContent()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = "Turbophrase", FontSize = 22, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Quick actions and status", Classes = { "muted" }, FontSize = 12 }
            }
        });

        foreach (var section in _sections)
        {
            stack.Children.Add(BuildSection(section));
        }

        return new Border
        {
            Classes = { "card" },
            Padding = new AThickness(16),
            Child = new ScrollViewer
            {
                MaxHeight = MaxHeight,
                Content = stack
            }
        };
    }

    private AControl BuildSection(TrayMenuSection section)
    {
        var rows = new StackPanel { Spacing = 6 };
        rows.Children.Add(new TextBlock { Text = section.Title, Classes = { "subtle" }, FontSize = 11, FontWeight = FontWeight.SemiBold });

        foreach (var item in section.Items)
        {
            rows.Children.Add(BuildRow(item));
        }

        return rows;
    }

    private AControl BuildRow(TrayMenuItem item)
    {
        var border = new Border
        {
            Classes = { "softCard" },
            Background = item.Checked ? new SolidColorBrush(AColor.FromRgb(24, 43, 72)) : Brush("TpSurfaceRaisedBrush"),
            BorderBrush = item.Checked ? Brush("TpAccent2Brush") : Brush("TpStrokeBrush"),
            Opacity = item.Enabled ? 1 : 0.48,
            Padding = new AThickness(12, 9),
            Cursor = item.Enabled && item.InvokeAsync != null ? new ACursor(StandardCursorType.Hand) : null,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = item.Text, FontWeight = FontWeight.SemiBold, FontSize = 13 },
                            string.IsNullOrWhiteSpace(item.Detail)
                                ? new TextBlock { IsVisible = false }
                                : new TextBlock { Text = item.Detail, Classes = { "muted" }, FontSize = 11 }
                        }
                    },
                    WithColumn(new TextBlock
                    {
                        Text = item.Checked ? "Current" : string.Empty,
                        Classes = { "subtle" },
                        FontSize = 10,
                        VerticalAlignment = VerticalAlignment.Center
                    }, 1)
                }
            }
        };

        if (item.Enabled && item.InvokeAsync != null)
        {
            border.PointerPressed += async (_, _) =>
            {
                Close();
                await item.InvokeAsync();
            };
        }

        return border;
    }

    private void PositionNearCursor()
    {
        var screen = DisplayPlacement.GetScreenNearCursor(Screens) ?? Screens.Primary;
        if (screen == null)
        {
            return;
        }

        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var area = screen.WorkingArea;
        var cursor = DisplayPlacement.GetCursorPixelPoint();
        var widthPx = (int)Math.Ceiling(Bounds.Width * scale);
        var heightPx = (int)Math.Ceiling(Bounds.Height * scale);

        Position = new PixelPoint(
            Math.Clamp(cursor.X - widthPx + 12, area.X + 8, area.Right - widthPx - 8),
            Math.Clamp(cursor.Y - 8, area.Y + 8, area.Bottom - heightPx - 8));
    }

    private static T WithColumn<T>(T control, int column) where T : AControl
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}

public sealed record TrayMenuSection(string Title, IReadOnlyList<TrayMenuItem> Items);

public sealed record TrayMenuItem(
    string Text,
    string? Detail = null,
    bool Checked = false,
    bool Enabled = true,
    Func<Task>? InvokeAsync = null);
