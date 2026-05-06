using System.Runtime.InteropServices;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

/// <summary>
/// Fast command-palette dialog for quickly choosing an operation.
/// </summary>
public sealed class PresetPickerDialog : Form
{
    private const int DialogWidth = 620;
    private const int ListTop = 140;
    private const int StatusGap = 10;
    private const int BottomPadding = 14;
    private const int RowHeight = 40;
    private const int RowGap = 5;
    private const int RowStride = RowHeight + RowGap;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly TextBox _filterTextBox;
    private readonly FlowLayoutPanel _operationList;
    private readonly Label _statusLabel;
    private readonly List<PickerOperation> _allOperations;
    private readonly List<OperationRow> _rows = new();
    private int _selectedIndex;
    private Point _dragStart;
    private bool _allowActivation;

    public PresetPickerDialog(IEnumerable<PickerOperation> operations)
    {
        Text = "Choose Operation";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = CommandSurfaceStyles.WindowBackground;
        ForeColor = CommandSurfaceStyles.Text;
        Padding = new Padding(1);
        DoubleBuffered = true;

        _allOperations = operations.ToList();
        var layout = CalculateLayout(_allOperations.Count);
        ClientSize = new Size(DialogWidth, layout.ClientHeight);

        var closeButton = CommandSurfaceStyles.CreateChromeButton("x", 574, 14, 30, 28);
        closeButton.Click += (_, _) => Cancel();

        var title = CommandSurfaceStyles.CreateLabel("Choose operation", 28, 20, 360, 28, CommandSurfaceStyles.Text, 16f, FontStyle.Bold);
        var subtitle = CommandSurfaceStyles.CreateLabel("Filter presets and commands, then press Enter.", 30, 50, 530, 20, CommandSurfaceStyles.MutedText, 9f);

        var inputFrame = CommandSurfaceStyles.CreateInputFrame(28, 82, 564, 46);
        _filterTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            BackColor = CommandSurfaceStyles.ElevatedSurface,
            ForeColor = CommandSurfaceStyles.Text,
            Font = CommandSurfaceStyles.UiFont(12.5f),
            PlaceholderText = "Search actions..."
        };
        inputFrame.Controls.Add(_filterTextBox);

        _operationList = new FlowLayoutPanel
        {
            Left = 28,
            Top = ListTop,
            Width = 564,
            Height = layout.ListHeight,
            BackColor = CommandSurfaceStyles.Surface,
            AutoScroll = layout.NeedsScroll,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        _statusLabel = CommandSurfaceStyles.CreateLabel("Type to filter. Up/Down moves. Enter selects. Esc cancels.", 30, layout.StatusTop, 560, 20, CommandSurfaceStyles.MutedText, 8.75f);

        Controls.AddRange([closeButton, title, subtitle, inputFrame, _operationList, _statusLabel]);

        _filterTextBox.TextChanged += (_, _) => ApplyFilter();
        Deactivate += (_, _) => ReactivateIfNeeded();

        Shown += (_, _) =>
        {
            if (_allowActivation)
            {
                FocusFilterBox();
            }
        };

        ApplyFilter();
    }

    public PickerOperation? SelectedOperation { get; private set; }

    public void SetCapturePending()
    {
        _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
        _statusLabel.Text = "Capturing selected text...";
    }

    public void SetCaptureReady()
    {
        _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
        _statusLabel.Text = "Text captured. Choose an operation.";
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
            FocusFilterBox();
            BeginInvoke(FocusFilterBox);
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
        if (e.Button == MouseButtons.Left && e.Y <= 82)
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Cancel();
            return true;
        }

        if (keyData == Keys.Enter)
        {
            SubmitSelectedItem();
            return true;
        }

        if (keyData == Keys.Down)
        {
            MoveSelection(1);
            return true;
        }

        if (keyData == Keys.Up)
        {
            MoveSelection(-1);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ApplyFilter()
    {
        var filter = _filterTextBox.Text.Trim();
        var numberedOperations = _allOperations
            .Select((operation, index) => operation with { Number = index + 1 })
            .ToList();
        var matches = string.IsNullOrWhiteSpace(filter)
            ? numberedOperations
            : numberedOperations
                .Where(item => MatchesFilter(item, filter))
                .ToList();

        _operationList.SuspendLayout();
        _operationList.Controls.Clear();
        _rows.Clear();

        for (var index = 0; index < matches.Count; index++)
        {
            var row = new OperationRow(matches[index], index);
            row.Clicked += (_, rowIndex) =>
            {
                _selectedIndex = rowIndex;
                UpdateSelection();
            };
            row.DoubleClicked += (_, rowIndex) =>
            {
                _selectedIndex = rowIndex;
                SubmitSelectedItem();
            };
            _rows.Add(row);
            _operationList.Controls.Add(row);
        }

        _selectedIndex = _rows.Count > 0 ? 0 : -1;
        UpdateSelection();
        _operationList.ResumeLayout();

        if (_rows.Count == 0)
        {
            _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
            _statusLabel.Text = "No matching operations.";
        }
        else
        {
            _statusLabel.ForeColor = CommandSurfaceStyles.MutedText;
            _statusLabel.Text = "Type to filter. Up/Down moves. Enter selects. Esc cancels.";
        }
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _rows.Count - 1);
        UpdateSelection();
        _operationList.ScrollControlIntoView(_rows[_selectedIndex]);
    }

    private void UpdateSelection()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Selected = i == _selectedIndex;
        }
    }

    private void SubmitSelectedItem()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
        {
            return;
        }

        SelectedOperation = _rows[_selectedIndex].Operation;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void FocusFilterBox()
    {
        if (IsDisposed)
        {
            return;
        }

        _filterTextBox.Focus();
        _filterTextBox.SelectAll();
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

    private static (int ClientHeight, int ListHeight, int StatusTop, bool NeedsScroll) CalculateLayout(int operationCount)
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var maxClientHeight = Math.Clamp(workingArea.Height - 80, 456, 820);
        var maxListHeight = Math.Max(RowStride * 5, maxClientHeight - ListTop - StatusGap - 20 - BottomPadding);
        var preferredListHeight = Math.Max(RowStride * Math.Max(operationCount, 1) + 2, RowStride * 5);
        var listHeight = Math.Min(preferredListHeight, maxListHeight);
        var statusTop = ListTop + listHeight + StatusGap;
        var clientHeight = statusTop + 20 + BottomPadding;

        return (clientHeight, listHeight, statusTop, preferredListHeight > listHeight);
    }

    private static bool MatchesFilter(PickerOperation operation, string filter)
    {
        if (operation.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || operation.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter.All(char.IsDigit)
            && operation.Number.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private sealed class OperationRow : Control
    {
        private bool _selected;

        public OperationRow(PickerOperation operation, int index)
        {
            Operation = operation;
            Index = index;
            Width = 540;
            Height = RowHeight;
            Margin = new Padding(0, 0, 0, RowGap);
            BackColor = CommandSurfaceStyles.Surface;
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
        }

        public event EventHandler<int>? Clicked;

        public event EventHandler<int>? DoubleClicked;

        public PickerOperation Operation { get; }

        public int Index { get; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Clicked?.Invoke(this, Index);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            DoubleClicked?.Invoke(this, Index);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(CommandSurfaceStyles.Surface);
            var background = Selected ? CommandSurfaceStyles.AccentSoft : CommandSurfaceStyles.ElevatedSurface;
            var border = Selected ? CommandSurfaceStyles.Accent : CommandSurfaceStyles.Border;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            CommandSurfaceStyles.DrawRoundedFill(e.Graphics, bounds, 10, background);
            using (var pen = new Pen(border, 1f))
            using (var path = CreatePath(bounds, 10))
            {
                e.Graphics.DrawPath(pen, path);
            }

            using var numberFont = CommandSurfaceStyles.UiFont(8.5f, FontStyle.Bold);
            using var titleFont = CommandSurfaceStyles.UiFont(10f, FontStyle.Bold);
            using var idFont = CommandSurfaceStyles.UiFont(8.25f);
            using var numberBrush = new SolidBrush(Selected ? CommandSurfaceStyles.Text : CommandSurfaceStyles.MutedText);
            using var titleBrush = new SolidBrush(CommandSurfaceStyles.Text);
            using var idBrush = new SolidBrush(CommandSurfaceStyles.MutedText);

            var number = Operation.Number > 0 ? Operation.Number.ToString() : "";
            e.Graphics.DrawString(number, numberFont, numberBrush, new RectangleF(14, 11, 28, 16));
            e.Graphics.DrawString(Operation.DisplayName, titleFont, titleBrush, new RectangleF(48, 5, Width - 68, 18));
            e.Graphics.DrawString(Operation.Id, idFont, idBrush, new RectangleF(48, 22, Width - 68, 14));
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreatePath(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

public sealed record PickerOperation(string Id, string DisplayName, HotkeyBinding Binding)
{
    public int Number { get; init; }

    public override string ToString() => Number > 0 ? $"{Number}. {DisplayName}" : DisplayName;
}
