using System.Runtime.InteropServices;

namespace Turbophrase.Services;

/// <summary>
/// Fast command-surface dialog for entering a one-off prompt before transforming selected text.
/// </summary>
public sealed class CustomPromptDialog : Form
{
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly TextBox _promptTextBox;
    private readonly ComboBox _providerComboBox;
    private readonly Label _statusLabel;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private Point _dragStart;
    private bool _allowActivation;

    public CustomPromptDialog(IEnumerable<string> providers, string defaultProvider)
    {
        Text = "Custom Prompt";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = CommandSurfaceStyles.WindowBackground;
        ForeColor = CommandSurfaceStyles.Text;
        ClientSize = new Size(640, 380);
        Padding = new Padding(1);
        DoubleBuffered = true;

        var closeButton = CommandSurfaceStyles.CreateChromeButton("x", 594, 14, 30, 28);
        closeButton.Click += (_, _) => Cancel();

        var title = CommandSurfaceStyles.CreateLabel("Custom prompt", 28, 22, 360, 30, CommandSurfaceStyles.Text, 18f, FontStyle.Bold);
        var subtitle = CommandSurfaceStyles.CreateLabel("Describe exactly how Turbophrase should transform the selected text.", 30, 54, 560, 22, CommandSurfaceStyles.MutedText, 9.5f);

        var promptFrame = CommandSurfaceStyles.CreateInputFrame(28, 96, 584, 176);
        _promptTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = true,
            BackColor = CommandSurfaceStyles.ElevatedSurface,
            ForeColor = CommandSurfaceStyles.Text,
            Font = CommandSurfaceStyles.UiFont(11.5f),
            WordWrap = true
        };
        promptFrame.Controls.Add(_promptTextBox);

        var providerLabel = CommandSurfaceStyles.CreateLabel("Provider", 30, 294, 100, 20, CommandSurfaceStyles.MutedText, 9f, FontStyle.Bold);
        _providerComboBox = new ComboBox
        {
            Left = 30,
            Top = 318,
            Width = 240,
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = CommandSurfaceStyles.ElevatedSurface,
            ForeColor = CommandSurfaceStyles.Text,
            FlatStyle = FlatStyle.Flat,
            Font = CommandSurfaceStyles.UiFont(10f)
        };

        foreach (var provider in providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _providerComboBox.Items.Add(provider);
        }

        if (_providerComboBox.Items.Count > 0)
        {
            var defaultIndex = _providerComboBox.FindStringExact(defaultProvider);
            _providerComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
        }

        _statusLabel = CommandSurfaceStyles.CreateLabel("Enter to add a new line. Ctrl+Enter runs. Esc cancels.", 292, 294, 320, 20, CommandSurfaceStyles.MutedText, 8.75f);
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;

        _okButton = CommandSurfaceStyles.CreateActionButton("Run", 422, 318, 88, 34, primary: true);
        _okButton.Click += (_, _) => Submit();

        _cancelButton = CommandSurfaceStyles.CreateActionButton("Cancel", 520, 318, 92, 34, primary: false);
        _cancelButton.Click += (_, _) => Cancel();

        Controls.AddRange([closeButton, title, subtitle, promptFrame, providerLabel, _providerComboBox, _statusLabel, _okButton, _cancelButton]);
        CancelButton = _cancelButton;

        _promptTextBox.KeyDown += OnPromptTextBoxKeyDown;
        Deactivate += (_, _) => ReactivateIfNeeded();

        Shown += (_, _) =>
        {
            if (_allowActivation)
            {
                FocusPromptBox();
            }
        };
    }

    public string PromptText => _promptTextBox.Text.Trim();

    public string? SelectedProvider => _providerComboBox.SelectedItem as string;

    public void SetCapturePending()
    {
        _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
        _statusLabel.Text = "Capturing selected text...";
    }

    public void SetCaptureReady()
    {
        _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
        _statusLabel.Text = "Ctrl+Enter runs. Esc cancels.";
    }

    public void SetCaptureFailed(string message)
    {
        _statusLabel.ForeColor = CommandSurfaceStyles.Danger;
        _statusLabel.Text = message;
    }

    public void ActivateForInput()
    {
        _allowActivation = true;
        if (Visible)
        {
            TopMost = false;
            TopMost = true;
            SetForegroundWindow(Handle);
            BringToFront();
            Activate();
            FocusPromptBox();
            BeginInvoke(FocusPromptBox);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        CommandSurfaceStyles.ApplyRoundedRegion(this, 18);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        CommandSurfaceStyles.ApplyRoundedRegion(this, 18);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(CommandSurfaceStyles.WindowBackground);
        CommandSurfaceStyles.DrawRoundedFill(e.Graphics, new Rectangle(1, 1, Width - 2, Height - 2), 18, CommandSurfaceStyles.Surface);
        CommandSurfaceStyles.DrawWindowBorder(e.Graphics, ClientRectangle, 18);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Y <= 86)
        {
            _dragStart = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left && _dragStart != Point.Empty)
        {
            Left += e.X - _dragStart.X;
            Top += e.Y - _dragStart.Y;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragStart = Point.Empty;
    }

    private void OnPromptTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            Submit();
            e.SuppressKeyPress = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter))
        {
            Submit();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            Cancel();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Submit()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void FocusPromptBox()
    {
        if (IsDisposed)
        {
            return;
        }

        _promptTextBox.Focus();
        _promptTextBox.SelectAll();
    }

    protected override bool ShowWithoutActivation => !_allowActivation;

    protected override void WndProc(ref Message m)
    {
        if (!_allowActivation && m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
    }

    private void ReactivateIfNeeded()
    {
        if (_allowActivation && Visible && !IsDisposed && DialogResult == DialogResult.None)
        {
            BeginInvoke(ActivateForInput);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
