using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AApplication = Avalonia.Application;
using ABrushes = Avalonia.Media.Brushes;
using AColor = Avalonia.Media.Color;
using AHorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using AThickness = Avalonia.Thickness;

namespace Turbophrase.Avalonia.Windows;

public sealed class ProcessingOverlayWindow : Window
{
    private const string BaseText = "Processing";
    private readonly TextBlock _label = new();
    private readonly DispatcherTimer _animationTimer;
    private int _dotCount;

    public ProcessingOverlayWindow()
    {
        Title = "Turbophrase processing";
        Width = 166;
        Height = 46;
        MinWidth = 140;
        MinHeight = 42;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowDecorations = WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        Background = ABrushes.Transparent;

        _label.Text = BaseText;
        _label.FontSize = 13;
        _label.FontWeight = FontWeight.SemiBold;
        _label.HorizontalAlignment = AHorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;

        Content = new Border
        {
            Classes = { "softCard" },
            Background = AApplication.Current?.FindResource("TpSurfaceRaisedBrush") as IBrush ?? new SolidColorBrush(AColor.FromRgb(18, 29, 48)),
            BorderBrush = AApplication.Current?.FindResource("TpStrokeStrongBrush") as IBrush ?? Brush("TpStrokeBrush"),
            CornerRadius = new CornerRadius(18),
            Padding = new AThickness(18, 10),
            Child = _label
        };

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _animationTimer.Tick += (_, _) =>
        {
            _dotCount = (_dotCount + 1) % 4;
            _label.Text = BaseText + new string('.', _dotCount);
        };
    }

    public void ShowOverlay()
    {
        _dotCount = 0;
        _label.Text = BaseText;
        PositionNearBottomRight();
        _animationTimer.Start();

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideOverlay()
    {
        _animationTimer.Stop();
        Hide();
    }

    private void PositionNearBottomRight()
    {
        var screen = Screens.Primary;
        var workingArea = screen?.WorkingArea;
        if (workingArea == null)
        {
            return;
        }

        Position = new PixelPoint(
            workingArea.Value.Right - (int)Math.Ceiling(Width) - 18,
            workingArea.Value.Bottom - (int)Math.Ceiling(Height) - 18);
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
