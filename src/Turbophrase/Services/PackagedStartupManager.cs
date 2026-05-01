using System.Reflection;

namespace Turbophrase.Services;

/// <summary>
/// MSIX-aware startup manager. Talks to
/// <c>Windows.ApplicationModel.StartupTask</c> via reflection so that the
/// unpackaged build can compile against this type without taking a hard
/// dependency on the Windows Runtime.
/// </summary>
/// <remarks>
/// MSIX-packaged apps are required to use this API instead of the registry
/// to enable startup behaviour, because Microsoft Store policy disallows
/// writing directly to <c>HKCU\...\Run</c>. The corresponding
/// <c>&lt;uap5:Extension Category="windows.startupTask"&gt;</c> entry must
/// be declared in <c>Package.appxmanifest</c> with a matching
/// <c>TaskId</c>; see <c>installer/msix/Package.appxmanifest</c>.
/// </remarks>
public sealed class PackagedStartupManager : IStartupManager
{
    private const string TaskId = "TurbophraseStartup";

    public bool IsEnabled()
    {
        var task = TryGetStartupTask();
        if (task == null)
        {
            return false;
        }

        var state = task.GetType().GetProperty("State")?.GetValue(task);
        return state?.ToString() is "Enabled" or "EnabledByPolicy";
    }

    public void Enable(string? configPath = null)
    {
        // MSIX startup tasks cannot accept per-launch arguments, so we
        // intentionally ignore configPath here. Power users who want a
        // non-default config under MSIX should set XDG_CONFIG_HOME.
        var task = TryGetStartupTask();
        if (task == null)
        {
            throw new InvalidOperationException(
                "Startup task 'TurbophraseStartup' is not declared in the Package manifest.");
        }

        var requestEnableMethod = task.GetType().GetMethod("RequestEnableAsync");
        if (requestEnableMethod == null)
        {
            throw new InvalidOperationException("StartupTask.RequestEnableAsync is unavailable on this platform.");
        }

        var asyncOp = requestEnableMethod.Invoke(task, null);
        if (asyncOp == null)
        {
            return;
        }

        // Block until the IAsyncOperation completes. We invoke GetResults
        // via reflection on the underlying type when GetAwaiter is unavailable.
        WaitOnAsync(asyncOp);
    }

    public void Disable()
    {
        var task = TryGetStartupTask();
        if (task == null)
        {
            return;
        }

        var disableMethod = task.GetType().GetMethod("Disable");
        disableMethod?.Invoke(task, null);
    }

    public string? Describe()
    {
        var task = TryGetStartupTask();
        if (task == null)
        {
            return null;
        }

        var state = task.GetType().GetProperty("State")?.GetValue(task);
        return $"MSIX StartupTask '{TaskId}' state={state}";
    }

    /// <summary>
    /// Returns the <c>StartupTask</c> WinRT object via reflection, or
    /// <c>null</c> when the API is unavailable (i.e., not running packaged).
    /// </summary>
    private static object? TryGetStartupTask()
    {
        try
        {
            var assembly = Assembly.Load("Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
            var type = assembly.GetType("Windows.ApplicationModel.StartupTask");
            if (type == null)
            {
                return null;
            }

            var getAsync = type.GetMethod("GetAsync", new[] { typeof(string) });
            if (getAsync == null)
            {
                return null;
            }

            var asyncOp = getAsync.Invoke(null, new object?[] { TaskId });
            return asyncOp == null ? null : WaitOnAsync(asyncOp);
        }
        catch
        {
            return null;
        }
    }

    private static object? WaitOnAsync(object asyncOp)
    {
        // IAsyncOperation<T> exposes GetResults(); IAsyncAction has a
        // void GetResults() too. Both also have a Completed event but
        // blocking via reflection is simpler for our needs.
        var type = asyncOp.GetType();
        var statusProp = type.GetProperty("Status");
        var getResults = type.GetMethod("GetResults");

        if (statusProp == null || getResults == null)
        {
            return null;
        }

        // Spin up to ~5 seconds.
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            var status = statusProp.GetValue(asyncOp)?.ToString();
            if (status == "Completed")
            {
                return getResults.Invoke(asyncOp, null);
            }
            if (status == "Error" || status == "Canceled")
            {
                return null;
            }
            Thread.Sleep(20);
        }

        return null;
    }
}
