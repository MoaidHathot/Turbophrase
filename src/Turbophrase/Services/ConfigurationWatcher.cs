using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

/// <summary>
/// Watches the configuration file for changes and triggers reload events.
/// </summary>
public class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounceTimer;
    private bool _pendingReload;

    /// <summary>
    /// Event raised when the configuration file changes.
    /// </summary>
    public event EventHandler? ConfigurationChanged;

    public ConfigurationWatcher()
    {
        var configDir = ConfigurationService.ConfigDirectory;
        var configFile = Path.GetFileName(ConfigurationService.ConfigFilePath);

        // Ensure directory exists before watching
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        _watcher = new FileSystemWatcher(configDir, configFile)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;

        // Debounce timer to avoid multiple rapid reloads (e.g., when editors save multiple times)
        _debounceTimer = new System.Timers.Timer(500) // 500ms debounce
        {
            AutoReset = false
        };
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Reset the debounce timer
        _pendingReload = true;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_pendingReload)
        {
            _pendingReload = false;
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounceTimer.Stop();
        _debounceTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
