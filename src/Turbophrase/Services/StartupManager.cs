using Microsoft.Win32;

namespace Turbophrase.Services;

/// <summary>
/// Manages Windows startup registration for Turbophrase.
/// </summary>
public static class StartupManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Turbophrase";

    /// <summary>
    /// Enables Turbophrase to run at Windows startup.
    /// </summary>
    /// <param name="configPath">Optional custom config path to use at startup.</param>
    public static void Enable(string? configPath = null)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Could not open registry key.");

        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine executable path.");

        var command = string.IsNullOrEmpty(configPath)
            ? $"\"{exePath}\""
            : $"\"{exePath}\" --config \"{Path.GetFullPath(configPath)}\"";

        key.SetValue(AppName, command);
    }

    /// <summary>
    /// Disables Turbophrase from running at Windows startup.
    /// </summary>
    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Checks if Turbophrase is configured to run at Windows startup.
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key?.GetValue(AppName) != null;
    }

    /// <summary>
    /// Gets the current startup command if registered.
    /// </summary>
    public static string? GetStartupCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key?.GetValue(AppName) as string;
    }
}
