namespace Turbophrase.Services;

/// <summary>
/// A small floating overlay window that indicates processing is in progress.
/// Appears in the bottom-right corner of the screen.
/// </summary>
public class ProcessingOverlay : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private int _dotCount;
    private const string BaseText = "Processing";

    public ProcessingOverlay()
    {
        // Form settings - small, borderless, topmost
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(45, 45, 48); // Dark background
        Size = new Size(140, 36);
        Opacity = 0.92;

        // Round corners
        Region = CreateRoundedRegion(Width, Height, 8);

        // Position in bottom-right corner, above taskbar
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetBounds(this);
        Location = new Point(
            workingArea.Right - Width - 16,
            workingArea.Bottom - Height - 16
        );

        // Spinner/processing label
        _label = new Label
        {
            Text = BaseText,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        Controls.Add(_label);

        // Animation timer for the dots
        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 400
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    /// <summary>
    /// Shows the overlay and starts the animation.
    /// </summary>
    public void ShowOverlay()
    {
        if (InvokeRequired)
        {
            BeginInvoke(ShowOverlay);
            return;
        }

        _dotCount = 0;
        _label.Text = BaseText;
        _animationTimer.Start();
        Show();
    }

    /// <summary>
    /// Hides the overlay and stops the animation.
    /// </summary>
    public void HideOverlay()
    {
        if (InvokeRequired)
        {
            BeginInvoke(HideOverlay);
            return;
        }

        _animationTimer.Stop();
        Hide();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _dotCount = (_dotCount + 1) % 4;
        _label.Text = BaseText + new string('.', _dotCount);
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
        path.AddArc(width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
        path.AddArc(width - radius * 2, height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(0, height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseAllFigures();
        return new Region(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    // Prevent the window from stealing focus
    protected override bool ShowWithoutActivation => true;

    // Make the window click-through (optional, can be removed if you want it clickable)
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT - click through
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW - don't show in Alt+Tab
            return cp;
        }
    }
}
