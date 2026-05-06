using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using AScreens = Avalonia.Controls.Screens;
using AScreen = Avalonia.Platform.Screen;

namespace Turbophrase.Avalonia.Windows;

internal static class DisplayPlacement
{
    public static AScreen? GetScreenNearCursor(AScreens screens)
    {
        var cursor = GetCursorPixelPoint();
        return screens.ScreenFromPoint(cursor) ?? screens.Primary;
    }

    public static PixelPoint GetCursorPixelPoint()
    {
        return GetCursorPos(out var point)
            ? new PixelPoint(point.X, point.Y)
            : new PixelPoint(0, 0);
    }

    public static void CenterOnScreen(Window window, AScreen screen)
    {
        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var widthPx = (int)Math.Ceiling(window.Width * scale);
        var heightPx = (int)Math.Ceiling(window.Height * scale);
        var area = screen.WorkingArea;

        window.Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - widthPx) / 2),
            area.Y + Math.Max(0, (area.Height - heightPx) / 2));
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }
}
