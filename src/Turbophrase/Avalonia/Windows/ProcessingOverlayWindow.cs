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
    private string _contextText = BaseText;
    private int _dotCount;

    public ProcessingOverlayWindow()
    {
        Title = "Turbophrase processing";
        Width = 260;
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
            _label.Text = _contextText + new string('.', _dotCount);
        };
    }

    public void ShowOverlay(string? context = null, IntPtr sourceWindowHandle = default)
    {
        _dotCount = 0;
        _contextText = string.IsNullOrWhiteSpace(context) ? BaseText : $"Processing {context}";
        _label.Text = _contextText;
        PositionNearBottomRight(sourceWindowHandle);
        _animationTimer.Start();

        if (!IsVisible)
        {
            Show();
        }

        Topmost = false;
        Topmost = true;
    }

    public void HideOverlay()
    {
        _animationTimer.Stop();
        Hide();
    }

    private void PositionNearBottomRight(IntPtr sourceWindowHandle)
    {
        var screen = DisplayPlacement.GetScreenForWindow(sourceWindowHandle, Screens)
            ?? DisplayPlacement.GetScreenNearCursor(Screens)
            ?? Screens.Primary;
        var workingArea = screen?.WorkingArea;
        if (workingArea == null)
        {
            return;
        }

        var scale = screen?.Scaling is > 0 ? screen.Scaling : 1;
        var widthPx = (int)Math.Ceiling(Width * scale);
        var heightPx = (int)Math.Ceiling(Height * scale);

        Position = new PixelPoint(
            workingArea.Value.Right - widthPx - 18,
            workingArea.Value.Bottom - heightPx - 18);
    }

    private static IBrush Brush(string key) => AApplication.Current?.FindResource(key) as IBrush ?? ABrushes.Transparent;
}
