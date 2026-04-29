using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

public static class RuntimeLog
{
    private static readonly Lock LogLock = new();
    private static volatile bool _isEnabled;

    public static string LogFilePath => Path.Combine(ConfigurationService.ConfigDirectory, "turbophrase.log");

    /// <summary>
    /// Gets whether diagnostic file logging is currently enabled.
    /// </summary>
    public static bool IsEnabled => _isEnabled;

    /// <summary>
    /// Applies logging configuration. When disabled, calls to <see cref="Write"/> are no-ops.
    /// </summary>
    public static void Configure(LoggingSettings settings)
    {
        _isEnabled = settings?.Enabled ?? false;
    }

    public static void Write(string message)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            lock (LogLock)
            {
                Directory.CreateDirectory(ConfigurationService.ConfigDirectory);
                File.AppendAllText(
                    LogFilePath,
                    $"{DateTimeOffset.Now:O} [pid:{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    public static void Clear()
    {
        try
        {
            lock (LogLock)
            {
                if (File.Exists(LogFilePath))
                {
                    File.Delete(LogFilePath);
                }
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}
