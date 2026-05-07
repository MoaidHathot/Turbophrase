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

    public static AScreen? GetScreenForWindow(IntPtr windowHandle, AScreens screens)
    {
        if (windowHandle == IntPtr.Zero || !GetWindowRect(windowHandle, out var rect))
        {
            return null;
        }

        var center = new PixelPoint(
            rect.Left + Math.Max(0, rect.Right - rect.Left) / 2,
            rect.Top + Math.Max(0, rect.Bottom - rect.Top) / 2);
        return screens.ScreenFromPoint(center) ?? screens.Primary;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
