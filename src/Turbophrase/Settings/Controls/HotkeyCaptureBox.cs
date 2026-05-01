namespace Turbophrase.Settings.Controls;

/// <summary>
/// Read-only TextBox that captures a key combination and renders it in the
/// same string form the rest of Turbophrase uses (for example
/// <c>Ctrl+Shift+G</c> or <c>Ctrl+Alt+;</c>).
/// </summary>
/// <remarks>
/// The control swallows the captured key press so the user does not also
/// trigger the underlying global hotkey while editing settings. Press
/// <c>Backspace</c> or <c>Delete</c> to clear the value.
/// </remarks>
public sealed class HotkeyCaptureBox : TextBox
{
    public HotkeyCaptureBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Default;
        BackColor = SystemColors.Window;
        ShortcutsEnabled = false;
        Text = string.Empty;
    }

    /// <summary>
    /// Raised whenever the captured combination changes.
    /// </summary>
    public event EventHandler? CapturedHotkeyChanged;

    /// <summary>
    /// Currently captured combination, or empty when none.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string CapturedHotkey
    {
        get => Text;
        set
        {
            Text = value ?? string.Empty;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Focused)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Backspace / Delete clears the field.
        if (keyData == Keys.Back || keyData == Keys.Delete)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                Text = string.Empty;
                CapturedHotkeyChanged?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        var keyCode = keyData & Keys.KeyCode;
        if (IsModifierKey(keyCode))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        var combo = TryFormat(keyData);
        if (combo == null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (Text != combo)
        {
            Text = combo;
            CapturedHotkeyChanged?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    private static bool IsModifierKey(Keys key) =>
        key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin
            or Keys.LMenu or Keys.RMenu or Keys.LControlKey or Keys.RControlKey
            or Keys.LShiftKey or Keys.RShiftKey or Keys.None;

    /// <summary>
    /// Formats a <see cref="Keys"/> bitfield into the string form the
    /// configuration parser understands. Returns <c>null</c> when the key is
    /// not a usable hotkey target.
    /// </summary>
    public static string? TryFormat(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        var keyName = TryGetKeyName(keyCode);
        if (keyName == null)
        {
            return null;
        }

        var parts = new List<string>(4);
        if ((keyData & Keys.Control) == Keys.Control)
        {
            parts.Add("Ctrl");
        }
        if ((keyData & Keys.Alt) == Keys.Alt)
        {
            parts.Add("Alt");
        }
        if ((keyData & Keys.Shift) == Keys.Shift)
        {
            parts.Add("Shift");
        }

        if (parts.Count == 0)
        {
            // Function keys are usable without a modifier; everything else
            // requires at least one modifier to register as a global hotkey.
            if (!(keyCode >= Keys.F1 && keyCode <= Keys.F24))
            {
                return null;
            }
        }

        parts.Add(keyName);
        return string.Join('+', parts);
    }

    private static string? TryGetKeyName(Keys keyCode)
    {
        // Letters and digits map directly to their character.
        if (keyCode >= Keys.A && keyCode <= Keys.Z)
        {
            return ((char)('A' + (keyCode - Keys.A))).ToString();
        }
        if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
        {
            return ((char)('0' + (keyCode - Keys.D0))).ToString();
        }
        if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
        {
            return ((char)('0' + (keyCode - Keys.NumPad0))).ToString();
        }
        if (keyCode >= Keys.F1 && keyCode <= Keys.F12)
        {
            return $"F{1 + (keyCode - Keys.F1)}";
        }

        return keyCode switch
        {
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Tab => "Tab",
            Keys.Escape => "Escape",
            Keys.Back => "Backspace",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.OemSemicolon => ";",
            Keys.Oemplus => "=",
            Keys.Oemcomma => ",",
            Keys.OemMinus => "-",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.Oemtilde => "`",
            Keys.OemOpenBrackets => "[",
            Keys.OemPipe => "\\",
            Keys.OemCloseBrackets => "]",
            Keys.OemQuotes => "'",
            _ => null,
        };
    }
}
