using Microsoft.Win32;

namespace Turbophrase.Services;

/// <summary>
/// Registers the running executable in <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// Used for unpackaged Turbophrase installs (winget, Inno Setup, portable ZIP).
/// </summary>
public sealed class Win32StartupManager : IStartupManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Turbophrase";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key?.GetValue(AppName) != null;
    }

    public void Enable(string? configPath = null)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException("Could not open registry key.");
        }

        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine executable path.");

        var command = string.IsNullOrEmpty(configPath)
            ? $"\"{exePath}\""
            : $"\"{exePath}\" --config \"{Path.GetFullPath(configPath)}\"";

        key.SetValue(AppName, command);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public string? Describe()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key?.GetValue(AppName) as string;
    }
}
