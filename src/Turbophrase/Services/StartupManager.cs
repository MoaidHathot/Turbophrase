namespace Turbophrase.Services;

/// <summary>
/// Static facade that callers continue to use for startup registration.
/// Selects between <see cref="Win32StartupManager"/> and
/// <see cref="PackagedStartupManager"/> at runtime.
/// </summary>
/// <remarks>
/// The selection runs once on first access. We probe for the WinRT
/// <c>Windows.ApplicationModel.Package.Current</c> indirectly: the Win32
/// implementation is correct for both unpackaged and packaged contexts
/// today, but Microsoft Store policy requires packaged apps to use the
/// <c>StartupTask</c> API. <see cref="PackagedStartupManager"/> returns
/// gracefully when the API is unavailable, so we attempt it first under
/// MSIX and fall back to the registry path otherwise.
/// </remarks>
public static class StartupManager
{
    private static readonly Lazy<IStartupManager> _impl = new(SelectImplementation);

    /// <summary>
    /// The selected implementation. Exposed for tests; production callers
    /// should use the static methods on this class.
    /// </summary>
    public static IStartupManager Implementation => _impl.Value;

    /// <inheritdoc cref="IStartupManager.IsEnabled"/>
    public static bool IsEnabled() => _impl.Value.IsEnabled();

    /// <inheritdoc cref="IStartupManager.Enable(string?)"/>
    public static void Enable(string? configPath = null) => _impl.Value.Enable(configPath);

    /// <inheritdoc cref="IStartupManager.Disable"/>
    public static void Disable() => _impl.Value.Disable();

    /// <summary>
    /// Returns the registered command line (Win32) or the MSIX startup task
    /// description, depending on the active implementation.
    /// </summary>
    public static string? GetStartupCommand() => _impl.Value.Describe();

    private static IStartupManager SelectImplementation()
    {
        if (IsRunningPackaged())
        {
            // Try the MSIX path first; if the StartupTask API is missing
            // for any reason, fall back to Win32.
            var packaged = new PackagedStartupManager();
            if (packaged.Describe() != null || TryGetPackageId() != null)
            {
                return packaged;
            }
        }

        return new Win32StartupManager();
    }

    /// <summary>
    /// Returns true when the current process has a Package identity (i.e.,
    /// installed via MSIX). Uses <c>GetCurrentPackageId</c> via P/Invoke so
    /// no WinRT reference is required at compile time.
    /// </summary>
    public static bool IsRunningPackaged() => TryGetPackageId() != null;

    private static string? TryGetPackageId()
    {
        try
        {
            uint length = 0;
            var rc = NativeMethods.GetCurrentPackageFullName(ref length, null);
            // ERROR_INSUFFICIENT_BUFFER (122) means "yes, there is a package
            // identity, here is its required buffer size". APPMODEL_ERROR_NO_PACKAGE
            // (15700) means "no package identity" -- i.e., we're unpackaged.
            if (rc == 15700 /* APPMODEL_ERROR_NO_PACKAGE */)
            {
                return null;
            }

            if (length == 0)
            {
                return null;
            }

            var buffer = new char[length];
            rc = NativeMethods.GetCurrentPackageFullName(ref length, buffer);
            if (rc != 0)
            {
                return null;
            }

            return new string(buffer, 0, (int)length - 1);
        }
        catch
        {
            return null;
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, char[]? packageFullName);
    }
}
