namespace Turbophrase.Services;

/// <summary>
/// Abstraction over "run at Windows startup" registration. There are two
/// implementations:
/// <list type="bullet">
///   <item><see cref="Win32StartupManager"/> writes the
///   <c>HKCU\...\Run</c> registry entry used by the unpackaged installer
///   and the portable ZIP.</item>
///   <item><see cref="PackagedStartupManager"/> talks to
///   <c>Windows.ApplicationModel.StartupTask</c> for MSIX-packaged installs
///   from the Microsoft Store.</item>
/// </list>
/// The correct implementation is selected at runtime by
/// <see cref="StartupManager"/> via a packaged-context probe.
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Whether Turbophrase is currently registered to run at startup.
    /// </summary>
    bool IsEnabled();

    /// <summary>
    /// Registers Turbophrase to run at startup. <paramref name="configPath"/>
    /// is honoured by the Win32 implementation when a custom config was
    /// passed via <c>--config</c>; the packaged implementation ignores it
    /// because MSIX startup tasks cannot carry per-user arguments.
    /// </summary>
    void Enable(string? configPath = null);

    /// <summary>
    /// Removes the startup registration.
    /// </summary>
    void Disable();

    /// <summary>
    /// Diagnostic string describing the current registration, or null when
    /// disabled. Used by the CLI <c>turbophrase startup</c> command.
    /// </summary>
    string? Describe();
}
