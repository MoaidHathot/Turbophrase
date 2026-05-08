using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AApplication = Avalonia.Application;
using ABrushes = Avalonia.Media.Brushes;
using AButton = Avalonia.Controls.Button;
using AControl = Avalonia.Controls.Control;
using AHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using AThickness = Avalonia.Thickness;

namespace Turbophrase.Avalonia.Windows;

public sealed class AppMessageWindow : Window
{
    public AppMessageWindow(string title, string message, bool isError)
    {
        Title = title;
        var screen = DisplayPlacement.GetScreenNearCursor(Screens) ?? Screens.Primary;
        var scale = screen?.Scaling is > 0 ? screen.Scaling : 1;
        var workingArea = screen?.WorkingArea;
        var availableWidth = workingArea?.Width / scale ?? 720;

        Width = Math.Clamp(availableWidth * 0.48, 420, 620);
        SizeToContent = SizeToContent.Height;
        MinWidth = 360;
        MaxHeight = workingArea?.Height / scale - 80 ?? 620;
        CanResize = true;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];
        Background = ABrushes.Transparent;

        var close = new AButton { Classes = { "primary" }, Content = "Close", MinWidth = 96 };
        close.Click += (_, _) => Close();

        Content = new Border
        {
            Background = AApplication.Current?.FindResource("TpAppBackground") as IBrush ?? Brush("TpVoidBrush"),
            Padding = new AThickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                RowSpacing = 16,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold },
                            new TextBlock
                            {
                                Text = isError ? "Turbophrase could not continue." : "Turbophrase",
                                Foreground = isError ? Brush("TpDangerBrush") : Brush("TpMutedTextBrush"),
                                FontSize = 13
                            }
                        }
                    },
                    WithRow(new Border
                    {
                        Classes = { "card" },
                        Padding = new AThickness(18),
                        Child = new ScrollViewer
                        {
                            MaxHeight = 360,
                            Content = new TextBlock { Text = message, Classes = { "muted" }, FontSize = 13 }
                        }
                    }, 1),
                    WithRow(new StackPanel
                    {
                        HorizontalAlignment = AHorizontalAlignment.Right,
                        Children = { close }
                    }, 2)
                }
            }
        };

        if (screen != null)
        {
            DisplayPlacement.CenterOnScreen(this, screen);
        }
    }

    private static T WithRow<T>(T control, int row) where T : AControl
    {
        Grid.SetRow(control, row);
        return control;
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
