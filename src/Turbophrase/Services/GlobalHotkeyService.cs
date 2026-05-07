using System.Runtime.InteropServices;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

/// <summary>
/// Service for registering and handling global hotkeys.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    // Windows API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier keys
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = new();
    private readonly IntPtr _windowHandle;
    private int _nextHotkeyId = 1;

    /// <summary>
    /// Event raised when a registered hotkey is pressed.
    /// </summary>
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public GlobalHotkeyService(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Registers a hotkey binding.
    /// </summary>
    /// <param name="binding">The hotkey binding to register.</param>
    /// <returns>True if registration succeeded, false otherwise.</returns>
    public bool RegisterHotkey(HotkeyBinding binding)
    {
        if (!TryParseHotkey(binding.Keys, out var modifiers, out var key))
        {
            System.Diagnostics.Debug.WriteLine($"[Hotkey] Failed to parse: '{binding.Keys}'");
            RuntimeLog.Write($"hotkey-parse-failed keys='{binding.Keys}'");
            return false;
        }

        var id = _nextHotkeyId++;
        if (RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            _registeredHotkeys[id] = binding;
            System.Diagnostics.Debug.WriteLine($"[Hotkey] Registered: '{binding.Keys}' -> {binding.Preset} (id={id}, mod=0x{modifiers:X}, key=0x{key:X})");
            RuntimeLog.Write($"hotkey-registered id={id} keys='{binding.Keys}' action='{binding.Action ?? "preset"}' preset='{binding.Preset}'");
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        System.Diagnostics.Debug.WriteLine($"[Hotkey] Failed to register: '{binding.Keys}' -> {binding.Preset} (mod=0x{modifiers:X}, key=0x{key:X}, error={error})");
        RuntimeLog.Write($"hotkey-register-failed keys='{binding.Keys}' action='{binding.Action ?? "preset"}' preset='{binding.Preset}' error={error}");
        return false;
    }

    /// <summary>
    /// Registers all hotkey bindings from configuration.
    /// </summary>
    /// <param name="bindings">The hotkey bindings to register.</param>
    /// <returns>List of successfully registered bindings.</returns>
    public List<HotkeyBinding> RegisterHotkeys(IEnumerable<HotkeyBinding> bindings)
    {
        var bindingsList = bindings.ToList();
        System.Diagnostics.Debug.WriteLine($"[Hotkey] Attempting to register {bindingsList.Count} hotkeys (handle=0x{_windowHandle:X})");
        
        var registered = new List<HotkeyBinding>();
        foreach (var binding in bindingsList)
        {
            if (RegisterHotkey(binding))
            {
                registered.Add(binding);
            }
        }
        return registered;
    }

    /// <summary>
    /// Handles WM_HOTKEY message from the message loop.
    /// </summary>
    /// <param name="hotkeyId">The hotkey ID from the message.</param>
    public void HandleHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var binding))
        {
            RuntimeLog.Write($"hotkey-pressed id={hotkeyId} keys='{binding.Keys}' action='{binding.Action ?? "preset"}' preset='{binding.Preset}'");
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(binding));
        }
        else
        {
            RuntimeLog.Write($"hotkey-pressed-unknown id={hotkeyId}");
        }
    }

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        var failed = 0;
        foreach (var (id, binding) in _registeredHotkeys)
        {
            if (!UnregisterHotKey(_windowHandle, id))
            {
                failed++;
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[Hotkey] Failed to unregister id={id} keys='{binding.Keys}' (error={error}). Likely called from a thread other than the one that registered it.");
                RuntimeLog.Write($"hotkey-unregister-failed id={id} keys='{binding.Keys}' error={error}");
            }
        }
        if (failed > 0)
        {
            RuntimeLog.Write($"hotkey-unregister-summary failed={failed} total={_registeredHotkeys.Count}");
        }
        _registeredHotkeys.Clear();
    }

    public static bool TryNormalizeHotkey(string hotkeyString, out string normalized, out string? error)
    {
        normalized = string.Empty;
        if (!TryParseHotkey(hotkeyString, out var modifiers, out var key, out error))
        {
            return false;
        }

        var parts = new List<string>();
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(GetKeyDisplayName(key));
        normalized = string.Join('+', parts);
        return true;
    }

    public static bool IsHotkeyAvailable(string hotkeyString, out string? error)
    {
        if (!TryParseHotkey(hotkeyString, out var modifiers, out var key, out error))
        {
            return false;
        }

        var id = Random.Shared.Next(0x4000, 0x7FFF);
        if (RegisterHotKey(IntPtr.Zero, id, modifiers, key))
        {
            UnregisterHotKey(IntPtr.Zero, id);
            error = null;
            return true;
        }

        error = $"Windows could not register this shortcut. It may already be in use. Error {Marshal.GetLastWin32Error()}.";
        return false;
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Shift+G" into modifiers and key code.
    /// </summary>
    public static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint key)
        => TryParseHotkey(hotkeyString, out modifiers, out key, out _);

    public static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint key, out string? error)
    {
        modifiers = 0;
        key = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            error = "Shortcut is required.";
            return false;
        }

        var parts = hotkeyString.Split('+').Select(p => p.Trim().ToLowerInvariant()).ToArray();
        var keyParts = 0;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                error = "Shortcut contains an empty key segment.";
                return false;
            }

            switch (part)
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (TryParseSpecialKey(part, out var specialKey))
                    {
                        key = specialKey;
                        keyParts++;
                    }
                    else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                    {
                        key = (uint)char.ToUpperInvariant(part[0]);
                        keyParts++;
                    }
                    else
                    {
                        error = $"Unsupported key '{part}'.";
                        return false;
                    }
                    break;
            }
        }

        if (key == 0 || keyParts == 0)
        {
            error = "Shortcut needs a non-modifier key.";
            return false;
        }

        if (keyParts > 1)
        {
            error = "Shortcut can only contain one non-modifier key.";
            return false;
        }

        if (modifiers == 0)
        {
            error = "Shortcut needs Ctrl, Alt, Shift, or Win.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses special key names like F1, Space, etc.
    /// </summary>
    private static bool TryParseSpecialKey(string keyName, out uint key)
    {
        key = keyName.ToLowerInvariant() switch
        {
            "f1" => 0x70,
            "f2" => 0x71,
            "f3" => 0x72,
            "f4" => 0x73,
            "f5" => 0x74,
            "f6" => 0x75,
            "f7" => 0x76,
            "f8" => 0x77,
            "f9" => 0x78,
            "f10" => 0x79,
            "f11" => 0x7A,
            "f12" => 0x7B,
            "space" => 0x20,
            "enter" => 0x0D,
            "tab" => 0x09,
            "escape" => 0x1B,
            "backspace" => 0x08,
            "delete" => 0x2E,
            "insert" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            ";" or "semicolon" => 0xBA,
            "=" or "equals" => 0xBB,
            "," or "comma" => 0xBC,
            "-" or "minus" => 0xBD,
            "." or "period" => 0xBE,
            "/" or "slash" => 0xBF,
            "`" or "backquote" or "backtick" => 0xC0,
            "[" or "leftbracket" => 0xDB,
            "\\" or "backslash" => 0xDC,
            "]" or "rightbracket" => 0xDD,
            "'" or "quote" or "apostrophe" => 0xDE,
            _ => 0
        };

        return key != 0;
    }

    private static string GetKeyDisplayName(uint key) => key switch
    {
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        0x70 => "F1",
        0x71 => "F2",
        0x72 => "F3",
        0x73 => "F4",
        0x74 => "F5",
        0x75 => "F6",
        0x76 => "F7",
        0x77 => "F8",
        0x78 => "F9",
        0x79 => "F10",
        0x7A => "F11",
        0x7B => "F12",
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x1B => "Escape",
        0x08 => "Backspace",
        0x2E => "Delete",
        0x2D => "Insert",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0xBA => "Semicolon",
        0xBB => "Equals",
        0xBC => "Comma",
        0xBD => "Minus",
        0xBE => "Period",
        0xBF => "Slash",
        0xC0 => "Backtick",
        0xDB => "LeftBracket",
        0xDC => "Backslash",
        0xDD => "RightBracket",
        0xDE => "Quote",
        _ => $"0x{key:X}",
    };

    public void Dispose()
    {
        UnregisterAll();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for hotkey pressed events.
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyBinding Binding { get; }

    public HotkeyPressedEventArgs(HotkeyBinding binding)
    {
        Binding = binding;
    }
}
