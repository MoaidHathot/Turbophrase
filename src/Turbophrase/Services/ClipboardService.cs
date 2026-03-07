using System.Runtime.InteropServices;

namespace Turbophrase.Services;

/// <summary>
/// Service for clipboard operations and simulating keyboard input.
/// Handles STA thread requirements for clipboard access.
/// </summary>
public class ClipboardService
{
    // Windows API imports for simulating keyboard input
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_MENU = 0x12; // Alt key
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x02;

    /// <summary>
    /// Gets the currently selected text by simulating Ctrl+C and reading from clipboard.
    /// </summary>
    /// <returns>The selected text, or null if no text is selected.</returns>
    public async Task<string?> GetSelectedTextAsync()
    {
        // Store original clipboard content
        var originalContent = await GetClipboardTextAsync();

        // Clear clipboard to detect if copy was successful
        await SetClipboardTextAsync(string.Empty);

        // Release any held modifier keys first (from the hotkey)
        ReleaseModifierKeys();

        // Small delay to ensure modifiers are released
        await Task.Delay(50);

        // Simulate Ctrl+C
        SimulateCopy();

        // Wait for copy operation to complete
        await Task.Delay(150);

        // Get the new clipboard content
        var selectedText = await GetClipboardTextAsync();

        // If clipboard is still empty, no text was selected
        if (string.IsNullOrEmpty(selectedText))
        {
            // Restore original clipboard content
            if (!string.IsNullOrEmpty(originalContent))
            {
                await SetClipboardTextAsync(originalContent);
            }
            return null;
        }

        return selectedText;
    }

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
        await Task.Delay(50);

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
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
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
        // Press Ctrl
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        // Press C
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        // Release C
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        // Release Ctrl
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>
    /// Simulates pressing Ctrl+V.
    /// </summary>
    private static void SimulatePaste()
    {
        // Press Ctrl
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        // Press V
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        // Release V
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        // Release Ctrl
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
