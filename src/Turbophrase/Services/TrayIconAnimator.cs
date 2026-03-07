namespace Turbophrase.Services;

/// <summary>
/// Handles animated tray icon states to indicate processing.
/// </summary>
public class TrayIconAnimator : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _defaultIcon;
    private readonly Icon[] _spinnerFrames;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private int _currentFrame;
    private bool _isAnimating;

    public TrayIconAnimator(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
        _defaultIcon = notifyIcon.Icon ?? SystemIcons.Application;
        _spinnerFrames = CreateSpinnerFrames();

        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 150 // ~6.7 fps for smooth animation
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    /// <summary>
    /// Starts the loading animation on the tray icon.
    /// </summary>
    public void StartAnimation()
    {
        if (_isAnimating) return;

        _isAnimating = true;
        _currentFrame = 0;
        _animationTimer.Start();
    }

    /// <summary>
    /// Stops the animation and restores the default icon.
    /// </summary>
    public void StopAnimation()
    {
        if (!_isAnimating) return;

        _animationTimer.Stop();
        _isAnimating = false;
        _notifyIcon.Icon = _defaultIcon;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!_isAnimating) return;

        _notifyIcon.Icon = _spinnerFrames[_currentFrame];
        _currentFrame = (_currentFrame + 1) % _spinnerFrames.Length;
    }

    /// <summary>
    /// Creates spinner animation frames (rotating dots pattern).
    /// </summary>
    private static Icon[] CreateSpinnerFrames()
    {
        const int frameCount = 8;
        const int size = 16;
        var frames = new Icon[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = CreateSpinnerFrame(size, i, frameCount);
        }

        return frames;
    }

    /// <summary>
    /// Creates a single frame of the spinner animation.
    /// </summary>
    private static Icon CreateSpinnerFrame(int size, int frame, int totalFrames)
    {
        var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var centerX = size / 2f;
            var centerY = size / 2f;
            var radius = size / 2f - 2;

            // Draw rotating arc
            var startAngle = (frame * 360f / totalFrames) - 90;
            var sweepAngle = 270f;

            using var pen = new Pen(Color.FromArgb(0, 120, 215), 2.5f); // Windows blue
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

            var rect = new RectangleF(
                centerX - radius,
                centerY - radius,
                radius * 2,
                radius * 2);

            g.DrawArc(pen, rect, startAngle, sweepAngle);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _animationTimer.Stop();
        _animationTimer.Dispose();

        foreach (var frame in _spinnerFrames)
        {
            frame?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
