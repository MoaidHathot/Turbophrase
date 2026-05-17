using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

public static class RuntimeLog
{
    private static readonly Lock LogLock = new();
    private static readonly Lock BootstrapLock = new();
    private static volatile bool _isEnabled;

    /// <summary>
    /// Path to the full, user-configurable runtime log. Only written to when
    /// <see cref="IsEnabled"/> is true (i.e. <c>logging.enabled</c> is set in
    /// turbophrase.json).
    /// </summary>
    public static string LogFilePath => Path.Combine(ConfigurationService.ConfigDirectory, "turbophrase.log");

    /// <summary>
    /// Path to the always-on bootstrap/crash log. Writes startup, shutdown and
    /// fatal-exception events independently of <c>logging.enabled</c> so first-
    /// run crashes (before any config is loaded) still leave a forensic trail.
    /// Lives in <c>%TEMP%</c> because the normal config directory may not yet
    /// be writable or may itself be the cause of the failure.
    /// </summary>
    public static string BootstrapLogFilePath =>
        Path.Combine(Path.GetTempPath(), "Turbophrase-bootstrap.log");

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

    /// <summary>
    /// Writes to the always-on bootstrap log regardless of the user's logging
    /// preference. Intended for fatal startup/shutdown events that the user
    /// needs to be able to recover even when their config is broken or
    /// missing. Best-effort and silent on failure.
    /// </summary>
    public static void WriteBootstrap(string message)
    {
        try
        {
            lock (BootstrapLock)
            {
                File.AppendAllText(
                    BootstrapLogFilePath,
                    $"{DateTimeOffset.Now:O} [pid:{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Bootstrap log is best-effort; never throw.
        }
    }

    /// <summary>
    /// Writes a full crash report (exception with stack trace) to a uniquely
    /// named file in <c>%TEMP%</c>, and appends a one-line summary to the
    /// bootstrap log. Returns the path to the crash file, or <c>null</c> if
    /// the write itself failed.
    /// </summary>
    public static string? WriteCrashDump(string source, Exception ex)
    {
        try
        {
            var fileName = $"Turbophrase-crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
            var path = Path.Combine(Path.GetTempPath(), fileName);

            var body =
                $"Turbophrase crash report{Environment.NewLine}" +
                $"Timestamp: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                $"Source:    {source}{Environment.NewLine}" +
                $"PID:       {Environment.ProcessId}{Environment.NewLine}" +
                $"OS:        {Environment.OSVersion}{Environment.NewLine}" +
                $"CLR:       {Environment.Version}{Environment.NewLine}" +
                $"CmdLine:   {Environment.CommandLine}{Environment.NewLine}" +
                $"---{Environment.NewLine}" +
                ex.ToString() + Environment.NewLine;

            File.WriteAllText(path, body);
            WriteBootstrap($"crash source='{source}' type='{ex.GetType().FullName}' message='{ex.Message}' dump='{path}'");
            return path;
        }
        catch
        {
            // Last-ditch: try to at least record the exception type in the bootstrap log.
            try
            {
                WriteBootstrap($"crash-dump-failed source='{source}' type='{ex.GetType().FullName}' message='{ex.Message}'");
            }
            catch
            {
                // Give up silently.
            }
            return null;
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
