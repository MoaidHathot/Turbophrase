using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

public static class RuntimeLog
{
    private static readonly Lock LogLock = new();

    public static string LogFilePath => Path.Combine(ConfigurationService.ConfigDirectory, "turbophrase.log");

    public static void Write(string message)
    {
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
