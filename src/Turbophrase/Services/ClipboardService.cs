using System.Runtime.InteropServices;

namespace Turbophrase.Services;

/// <summary>
/// Service for clipboard operations and simulating keyboard input.
/// Handles STA thread requirements for clipboard access.
/// </summary>
public class ClipboardService
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // Windows API imports for simulating keyboard input
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_MENU = 0x12; // Alt key
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RWIN = 0x5C;
    private const ushort VK_C = 0x43;
    private const ushort VK_INSERT = 0x2D;
    private const ushort VK_V = 0x56;

    /// <summary>
    /// Gets the current foreground window handle.
    /// </summary>
    public IntPtr GetActiveWindowHandle() => GetForegroundWindow();

    /// <summary>
    /// Attempts to restore focus to a window before pasting transformed text back.
    /// </summary>
    public void RestoreWindowFocus(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
        {
            SetForegroundWindow(windowHandle);
        }
    }

    /// <summary>
    /// Captures the currently selected text while preserving the text that was on the clipboard before capture.
    /// </summary>
    public async Task<ClipboardCaptureResult> CaptureSelectedTextAsync()
    {
        var originalContent = await GetClipboardTextAsync();

        // Release any held modifier keys first (from the hotkey)
        ReleaseModifierKeys();

        // Small delay to ensure modifiers are released
        await WaitForModifiersReleasedAsync();

        foreach (var copyAttempt in GetCopyAttempts())
        {
            // Clear clipboard to detect if copy was successful
            await SetClipboardTextAsync(string.Empty);
            RuntimeLog.Write($"selection-copy-attempt method='{copyAttempt.Name}'");

            copyAttempt.Action();

            // Wait for copy operation to complete
            await Task.Delay(150);

            var selectedText = await GetClipboardTextAsync();
            if (!string.IsNullOrEmpty(selectedText))
            {
                RuntimeLog.Write($"selection-copy-success method='{copyAttempt.Name}' length={selectedText.Length}");
                return ClipboardCaptureResult.Success(selectedText, originalContent);
            }
        }

        RuntimeLog.Write("selection-copy-failed no-text-copied");

        await RestoreClipboardTextAsync(originalContent);
        return ClipboardCaptureResult.Fail(originalContent);
    }

    /// <summary>
    /// Gets the currently selected text by simulating Ctrl+C and reading from clipboard.
    /// </summary>
    /// <returns>The selected text, or null if no text is selected.</returns>
    public async Task<string?> GetSelectedTextAsync() => (await CaptureSelectedTextAsync()).SelectedText;

    public Task RestoreClipboardTextAsync(string? text) => SetClipboardTextAsync(text ?? string.Empty);

    /// <summary>
    /// Replaces the currently selected text with new text by setting clipboard and simulating Ctrl+V.
    /// </summary>
    /// <param name="newText">The text to paste.</param>
    public async Task ReplaceSelectedTextAsync(string newText)
    {
        // Set the new text to clipboard
        await SetClipboardTextAsync(newText);

        // Small delay to ensure clipboard is set
        await Task.Delay(50);

        // Release any held modifier keys
        ReleaseModifierKeys();

        // Small delay
        await WaitForModifiersReleasedAsync();

        // Simulate Ctrl+V
        SimulatePaste();

        // Wait for paste operation to complete
        await Task.Delay(150);
    }

    /// <summary>
    /// Releases modifier keys that might still be held from the hotkey press.
    /// </summary>
    private static void ReleaseModifierKeys()
    {
        // Release Ctrl, Shift, Alt if they're pressed
        SendKeyUp(VK_CONTROL);
        SendKeyUp(VK_SHIFT);
        SendKeyUp(VK_MENU);
        SendKeyUp(VK_LWIN);
        SendKeyUp(VK_RWIN);
    }

    private static async Task WaitForModifiersReleasedAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!IsPressed(VK_CONTROL) && !IsPressed(VK_SHIFT) && !IsPressed(VK_MENU)
                && !IsPressed(VK_LWIN) && !IsPressed(VK_RWIN))
            {
                await Task.Delay(30);
                return;
            }

            ReleaseModifierKeys();
            await Task.Delay(30);
        }

        await Task.Delay(80);
    }

    private static bool IsPressed(ushort virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static void SendKeyUp(ushort virtualKey)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = KEYEVENTF_KEYUP,
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Gets text from the clipboard (handles STA thread requirement).
    /// </summary>
    private static Task<string?> GetClipboardTextAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        var thread = new Thread(() =>
        {
            try
            {
                // Retry a few times in case clipboard is locked
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            tcs.SetResult(Clipboard.GetText());
                            return;
                        }
                        else
                        {
                            tcs.SetResult(null);
                            return;
                        }
                    }
                    catch (ExternalException)
                    {
                        Thread.Sleep(50);
                    }
                }
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return tcs.Task;
    }

    /// <summary>
    /// Sets text to the clipboard (handles STA thread requirement).
    /// </summary>
    private static Task SetClipboardTextAsync(string text)
    {
        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                // Retry a few times in case clipboard is locked
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(text))
                        {
                            Clipboard.Clear();
                        }
                        else
                        {
                            Clipboard.SetText(text);
                        }
                        tcs.SetResult(true);
                        return;
                    }
                    catch (ExternalException)
                    {
                        Thread.Sleep(50);
                    }
                }
                tcs.SetResult(false);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return tcs.Task;
    }

    /// <summary>
    /// Simulates pressing Ctrl+C.
    /// </summary>
    private static void SimulateCopy()
    {
        SendModifiedKey(VK_CONTROL, VK_C);
    }

    private static void SimulateCopyWithCtrlInsert()
    {
        SendModifiedKey(VK_CONTROL, VK_INSERT);
    }

    private static void SimulateCopyWithCtrlShiftC()
    {
        keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)VK_SHIFT, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)VK_C, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>
    /// Simulates pressing Ctrl+V.
    /// </summary>
    private static void SimulatePaste()
    {
        SendModifiedKey(VK_CONTROL, VK_V);
    }

    private static void SendModifiedKey(ushort modifier, ushort key)
    {
        keybd_event((byte)modifier, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)key, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static IEnumerable<(string Name, Action Action)> GetCopyAttempts()
    {
        yield return ("ctrl+c", SimulateCopy);
        yield return ("ctrl+insert", SimulateCopyWithCtrlInsert);
        yield return ("ctrl+shift+c", SimulateCopyWithCtrlShiftC);
    }
}

public sealed record ClipboardCaptureResult(bool Succeeded, string? SelectedText, string? OriginalClipboardText)
{
    public static ClipboardCaptureResult Success(string selectedText, string? originalClipboardText)
        => new(true, selectedText, originalClipboardText);

    public static ClipboardCaptureResult Fail(string? originalClipboardText)
        => new(false, null, originalClipboardText);
}
