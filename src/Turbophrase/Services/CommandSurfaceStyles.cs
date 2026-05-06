using System.Drawing.Drawing2D;

namespace Turbophrase.Services;

internal static class CommandSurfaceStyles
{
    public static readonly Color WindowBackground = Color.FromArgb(12, 16, 23);
    public static readonly Color Surface = Color.FromArgb(18, 24, 34);
    public static readonly Color ElevatedSurface = Color.FromArgb(24, 31, 43);
    public static readonly Color Border = Color.FromArgb(48, 58, 76);
    public static readonly Color BorderStrong = Color.FromArgb(86, 103, 132);
    public static readonly Color Text = Color.FromArgb(244, 247, 251);
    public static readonly Color MutedText = Color.FromArgb(156, 168, 188);
    public static readonly Color Accent = Color.FromArgb(125, 92, 255);
    public static readonly Color AccentSoft = Color.FromArgb(42, 35, 81);
    public static readonly Color Danger = Color.FromArgb(255, 105, 115);

    public static Font UiFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static Label CreateLabel(string text, int left, int top, int width, int height, Color color, float size, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            ForeColor = color,
            BackColor = Color.Transparent,
            Font = UiFont(size, style),
            AutoEllipsis = true,
            UseMnemonic = false
        };
    }

    public static Button CreateChromeButton(string text, int left, int top, int width, int height)
    {
        var button = new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = MutedText,
            Font = UiFont(10f, FontStyle.Regular),
            TabStop = false,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 48, 64);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 63, 83);
        return button;
    }

    public static Button CreateActionButton(string text, int left, int top, int width, int height, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : ElevatedSurface,
            ForeColor = Text,
            Font = UiFont(9.5f, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(141, 113, 255) : Color.FromArgb(35, 44, 60);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(104, 74, 230) : Color.FromArgb(42, 52, 70);
        return button;
    }

    public static Panel CreateInputFrame(int left, int top, int width, int height)
    {
        var panel = new Panel
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            BackColor = ElevatedSurface,
            Padding = new Padding(12, 10, 12, 10)
        };

        panel.Paint += (_, e) => DrawRoundedBorder(e.Graphics, panel.ClientRectangle, 12, Border);
        return panel;
    }

    public static void ApplyRoundedRegion(Form form, int radius)
    {
        if (form.Width <= 0 || form.Height <= 0)
        {
            return;
        }

        var previous = form.Region;
        form.Region = CreateRoundedRegion(new Rectangle(0, 0, form.Width, form.Height), radius);
        previous?.Dispose();
    }

    public static void DrawWindowBorder(Graphics graphics, Rectangle bounds, int radius)
    {
        var borderBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        DrawRoundedBorder(graphics, borderBounds, radius, BorderStrong);
    }

    public static void DrawRoundedFill(Graphics graphics, Rectangle bounds, int radius, Color color)
    {
        using var path = CreateRoundedPath(bounds, radius);
        using var brush = new SolidBrush(color);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedBorder(Graphics graphics, Rectangle bounds, int radius, Color color)
    {
        using var path = CreateRoundedPath(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), radius);
        using var pen = new Pen(color, 1f);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawPath(pen, path);
    }

    private static Region CreateRoundedRegion(Rectangle bounds, int radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        return new Region(path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
