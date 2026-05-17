using Turbophrase;
using Turbophrase.Avalonia;
using Turbophrase.Avalonia.Windows;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;
using System.Reflection;

/// <summary>
/// Turbophrase - AI-powered text transformation tool.
/// </summary>
static class Program
{
    private const string SingleInstanceMutexName = @"Local\Turbophrase.SingleInstance";

    /// <summary>
    /// Product version, read from the assembly's <see cref="AssemblyInformationalVersionAttribute"/>.
    /// The version itself is defined centrally in &lt;RepoRoot&gt;/Directory.Build.props.
    /// </summary>
    private static string ProductVersion
    {
        get
        {
            var informational = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrEmpty(informational))
            {
                // The .NET SDK appends "+<gitsha>" when building from a git repo.
                // Strip it so the user-facing version stays clean (e.g. "1.0.6").
                var plusIndex = informational.IndexOf('+');
                return plusIndex >= 0 ? informational[..plusIndex] : informational;
            }

            return typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    [STAThread]
    static async Task<int> Main(string[] args)
    {
        // Install global unhandled-exception handlers as early as possible so
        // that any failure during startup leaves a forensic trail in
        // %TEMP%\Turbophrase-crash-*.log and %TEMP%\Turbophrase-bootstrap.log,
        // even when logging.enabled is false (the default) or when the config
        // file itself is the cause of the failure.
        InstallGlobalExceptionHandlers();
        RuntimeLog.WriteBootstrap($"main-start version={ProductVersion} args=[{string.Join(' ', args)}]");

        try
        {
            return await MainCore(args);
        }
        catch (Exception ex)
        {
            HandleFatalException("Program.Main", ex);
            return 1;
        }
        finally
        {
            RuntimeLog.WriteBootstrap("main-exit");
        }
    }

    private static async Task<int> MainCore(string[] args)
    {
        // Enable high DPI support for proper icon scaling
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Honor portable-mode placement: if a turbophrase.json (or legacy
        // config.json) sits next to the executable, prefer it over the
        // %APPDATA% default. An explicit --config below still wins.
        TryEnablePortableConfig();

        // Register the Win32-backed secrets resolver so @credman: references
        // in turbophrase.json are expanded by ConfigurationService.
        ConfigurationService.SetSecretsResolver(new SecretsStore());

        // Parse --config and --init-config arguments
        string? customConfigPath = null;
        bool initConfigIfMissing = false;
        var remainingArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                customConfigPath = args[i + 1];
                i++; // Skip the next argument (the path)
            }
            else if (args[i].StartsWith("--config="))
            {
                customConfigPath = args[i].Substring("--config=".Length);
            }
            else if (args[i] == "--init-config")
            {
                initConfigIfMissing = true;
            }
            else
            {
                remainingArgs.Add(args[i]);
            }
        }

        // Set custom config path if provided
        if (!string.IsNullOrEmpty(customConfigPath))
        {
            var fullPath = Path.GetFullPath(customConfigPath);
            
            if (!File.Exists(fullPath))
            {
                if (initConfigIfMissing)
                {
                    // Create the config file with default values
                    try
                    {
                        var directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        File.WriteAllText(fullPath, ConfigurationService.GetDefaultConfigJson());
                        Console.WriteLine($"Configuration file created at: {fullPath}");
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to create configuration file at: {fullPath}\n\n{ex.Message}";
                        Console.Error.WriteLine(errorMsg);
                        
                        // Show a styled dialog if running as GUI (no CLI commands)
                        if (remainingArgs.Count == 0)
                        {
                            await ShowStartupMessageAsync("Turbophrase Error", errorMsg, isError: true);
                        }
                        return 1;
                    }
                }
                else
                {
                    var errorMsg = $"Configuration file not found: {fullPath}\n\nUse --init-config to create it with default values.";
                    Console.Error.WriteLine(errorMsg);
                    
                    // Show a styled dialog if running as GUI (no CLI commands)
                    if (remainingArgs.Count == 0)
                    {
                        await ShowStartupMessageAsync("Turbophrase Error", errorMsg, isError: true);
                    }
                    return 1;
                }
            }
            
            ConfigurationService.SetCustomConfigPath(fullPath);
        }

        // Handle CLI commands
        if (remainingArgs.Count > 0)
        {
            return await HandleCliCommandAsync(remainingArgs.ToArray());
        }

        using var singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("Turbophrase is already running.");
            RuntimeLog.WriteBootstrap("already-running single-instance-mutex-blocked");
            return 1;
        }

        // Run as tray application
        ApplicationConfiguration.Initialize();
        RuntimeLog.WriteBootstrap($"tray-run-begin config='{ConfigurationService.ConfigFilePath}'");
        try
        {
            Application.Run(new TrayApplicationContext());
            RuntimeLog.WriteBootstrap("tray-run-end");
            return 0;
        }
        catch (Exception ex)
        {
            HandleFatalException("Application.Run", ex);
            return 1;
        }
    }

    /// <summary>
    /// Registers process-wide handlers for unhandled exceptions on every
    /// surface that can throw silently in a WinForms+Avalonia hybrid:
    /// the AppDomain, the WinForms message pump, and the TPL unobserved-task
    /// channel. Each handler writes a crash dump to %TEMP% so post-mortem
    /// diagnosis is possible even with logging disabled.
    /// </summary>
    private static void InstallGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleFatalException("AppDomain.UnhandledException", ex);
            }
            else
            {
                RuntimeLog.WriteBootstrap($"appdomain-unhandled non-exception='{e.ExceptionObject}'");
            }
        };

        Application.ThreadException += (_, e) =>
        {
            HandleFatalException("Application.ThreadException", e.Exception);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // Log but mark observed so the process is not torn down for a
            // background unobserved task exception.
            RuntimeLog.WriteCrashDump("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void HandleFatalException(string source, Exception ex)
    {
        var dumpPath = RuntimeLog.WriteCrashDump(source, ex);
        try
        {
            Console.Error.WriteLine($"Turbophrase fatal error ({source}): {ex.Message}");
            if (dumpPath != null)
            {
                Console.Error.WriteLine($"Crash dump: {dumpPath}");
            }
        }
        catch
        {
            // No console attached; ignore.
        }

        try
        {
            var body = $"Turbophrase encountered a fatal error and must close.\n\n" +
                       $"Source: {source}\n" +
                       $"Error:  {ex.GetType().Name}: {ex.Message}\n\n" +
                       (dumpPath != null
                            ? $"A full crash report was written to:\n{dumpPath}"
                            : "Crash report could not be written.");

            MessageBox.Show(
                body,
                "Turbophrase - Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.DefaultDesktopOnly);
        }
        catch
        {
            // If even MessageBox fails, we've done all we can.
        }
    }

    /// <summary>
    /// If the executable directory contains <c>turbophrase.json</c> (or the
    /// legacy <c>config.json</c>), wire it up as the active configuration
    /// path. This gives portable distributions a self-contained config
    /// without requiring users to pass <c>--config</c>. An explicit
    /// <c>--config</c> argument processed later still wins.
    /// </summary>
    private static void TryEnablePortableConfig()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            var exeDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(exeDir))
            {
                return;
            }

            var portablePreferred = Path.Combine(exeDir, "turbophrase.json");
            var portableLegacy = Path.Combine(exeDir, "config.json");

            string? portablePath = File.Exists(portablePreferred) ? portablePreferred
                                 : File.Exists(portableLegacy) ? portableLegacy
                                 : null;

            if (portablePath != null)
            {
                ConfigurationService.SetCustomConfigPath(portablePath);
                RuntimeLog.WriteBootstrap($"portable-config-detected path='{portablePath}'");
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.WriteBootstrap($"portable-config-probe-failed error='{ex.Message}'");
        }
    }

    private static async Task<int> HandleCliCommandAsync(string[] args)
    {
        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "init":
                return InitCommand();

            case "config":
                return ConfigCommand();

            case "test":
                return await TestCommandAsync(args.Length > 1 ? args[1] : null);

            case "startup":
                return StartupCommand(args.Skip(1).ToArray());

            case "settings":
                return await SettingsCommandAsync();

            case "setup":
            case "provider-setup":
                return await ProviderSetupCommandAsync();

            case "secrets":
                return SecretsCommand(args.Skip(1).ToArray());

            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                return 0;

            case "version":
            case "--version":
            case "-v":
                Console.WriteLine($"Turbophrase v{ProductVersion}");
                return 0;

            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintHelp();
                return 1;
        }
    }

    private static Task ShowStartupMessageAsync(string title, string message, bool isError)
    {
        return AvaloniaUiHost.ShowStandaloneWindowAsync(() => new AppMessageWindow(title, message, isError));
    }

    private static int InitCommand()
    {
        Console.WriteLine("Initializing Turbophrase configuration...");

        try
        {
            ConfigurationService.InitializeConfigFile();
            Console.WriteLine($"Configuration file created at: {ConfigurationService.ConfigFilePath}");
            Console.WriteLine();
            Console.WriteLine("Edit the configuration file to set your API keys and customize presets.");
            Console.WriteLine("Environment variable syntax is supported: ${OPENAI_API_KEY}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating configuration: {ex.Message}");
            return 1;
        }
    }

    private static int ConfigCommand()
    {
        if (!File.Exists(ConfigurationService.ConfigFilePath))
        {
            Console.Error.WriteLine("Configuration file not found. Run 'turbophrase init' first.");
            return 1;
        }

        Console.WriteLine($"Configuration file: {ConfigurationService.ConfigFilePath}");
        Console.WriteLine();

        try
        {
            var config = ConfigurationService.LoadConfiguration();

            Console.WriteLine("Providers:");
            foreach (var (name, provider) in config.Providers)
            {
                var hasKey = !string.IsNullOrEmpty(provider.ApiKey) && !provider.ApiKey.StartsWith("${");
                var status = hasKey || provider.Type is "copilot" or "copilot-cli" or "github-copilot" or "ollama"
                    ? "[configured]"
                    : "[not configured]";
                Console.WriteLine($"  {name} ({provider.Type}) {status}");
            }

            Console.WriteLine();
            Console.WriteLine($"Default provider: {config.DefaultProvider}");

            Console.WriteLine();
            Console.WriteLine("Presets:");
            foreach (var (name, preset) in config.Presets)
            {
                Console.WriteLine($"  {name}: {preset.Name}");
            }

            Console.WriteLine();
            Console.WriteLine("Hotkeys:");
            foreach (var hotkey in config.Hotkeys)
            {
                Console.WriteLine($"  {hotkey.Keys} -> {hotkey.Preset}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading configuration: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TestCommandAsync(string? providerName)
    {
        Console.WriteLine("Testing provider connection...");

        try
        {
            var config = ConfigurationService.LoadConfiguration();
            var orchestrator = new TextTransformOrchestrator(config);

            var targetProvider = providerName ?? config.DefaultProvider;

            Console.WriteLine($"Testing provider: {targetProvider}");

            var result = await orchestrator.TestProviderAsync(targetProvider);

            if (result.Success)
            {
                Console.WriteLine($"Success! Response: {result.TransformedText}");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"Failed: {result.ErrorMessage}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int SecretsCommand(string[] args)
    {
        var store = new SecretsStore();

        if (args.Length == 0 || args[0] == "list")
        {
            var names = store.List();
            if (names.Count == 0)
            {
                Console.WriteLine("No secrets stored.");
                return 0;
            }

            Console.WriteLine("Stored secrets (target prefix: 'Turbophrase:'):");
            foreach (var name in names)
            {
                Console.WriteLine($"  {name}");
            }
            return 0;
        }

        switch (args[0])
        {
            case "set":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: turbophrase secrets set <name> [value]");
                    Console.Error.WriteLine("If <value> is omitted, the secret is read from stdin.");
                    return 1;
                }
                {
                    var name = args[1];
                    string value;
                    if (args.Length >= 3)
                    {
                        value = args[2];
                    }
                    else
                    {
                        Console.Error.Write("Enter secret (input is hidden): ");
                        value = ReadHiddenLine();
                        Console.Error.WriteLine();
                    }

                    try
                    {
                        store.Save(name, value);
                        Console.WriteLine($"Saved secret 'Turbophrase:{name}'.");
                        Console.WriteLine($"Reference it from turbophrase.json as: \"@credman:{name}\"");
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed: {ex.Message}");
                        return 1;
                    }
                }

            case "get":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: turbophrase secrets get <name>");
                    return 1;
                }
                {
                    var value = store.TryRead(args[1]);
                    if (value == null)
                    {
                        Console.Error.WriteLine("Secret not found.");
                        return 1;
                    }
                    Console.WriteLine(value);
                    return 0;
                }

            case "remove":
            case "delete":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: turbophrase secrets remove <name>");
                    return 1;
                }
                try
                {
                    var existed = store.Delete(args[1]);
                    Console.WriteLine(existed ? "Removed." : "Secret was not present.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed: {ex.Message}");
                    return 1;
                }

            default:
                Console.Error.WriteLine($"Unknown secrets subcommand: {args[0]}");
                Console.Error.WriteLine("Usage: turbophrase secrets [list|get <name>|set <name> [value]|remove <name>]");
                return 1;
        }
    }

    private static string ReadHiddenLine()
    {
        var sb = new System.Text.StringBuilder();
        ConsoleKeyInfo info;
        while ((info = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (info.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Length--;
                }
            }
            else if (!char.IsControl(info.KeyChar))
            {
                sb.Append(info.KeyChar);
            }
        }
        return sb.ToString();
    }

    private static async Task<int> SettingsCommandAsync()
    {
        // Launches the Settings UI as a one-shot foreground window. Useful when
        // the tray app isn't running (e.g., from a fresh terminal). When the
        // tray IS running, users typically open Settings from the tray menu.
        try
        {
            await AvaloniaUiHost.ShowStandaloneWindowAsync(() => new SettingsWindow());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ProviderSetupCommandAsync()
    {
        try
        {
            var saved = await FirstRunWindow.ShowProviderSetupAsync();
            Console.WriteLine(saved ? "Provider setup saved." : "Provider setup cancelled.");
            return saved ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int StartupCommand(string[] args)
    {
        if (args.Length == 0 || args[0] == "--status")
        {
            var isEnabled = StartupManager.IsEnabled();
            Console.WriteLine($"Run at startup: {(isEnabled ? "Enabled" : "Disabled")}");
            if (isEnabled)
            {
                Console.WriteLine($"Command: {StartupManager.GetStartupCommand()}");
            }
            return 0;
        }

        switch (args[0])
        {
            case "--enable":
                try
                {
                    StartupManager.Enable(ConfigurationService.CustomConfigFilePath);
                    Console.WriteLine("Turbophrase will now run at Windows startup.");
                    if (!string.IsNullOrEmpty(ConfigurationService.CustomConfigFilePath))
                        Console.WriteLine($"Using config: {ConfigurationService.CustomConfigFilePath}");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to enable startup: {ex.Message}");
                    return 1;
                }

            case "--disable":
                try
                {
                    StartupManager.Disable();
                    Console.WriteLine("Turbophrase will no longer run at Windows startup.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to disable startup: {ex.Message}");
                    return 1;
                }

            default:
                Console.Error.WriteLine($"Unknown startup option: {args[0]}");
                Console.Error.WriteLine("Usage: turbophrase startup [--enable|--disable|--status]");
                return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Turbophrase - AI-powered text transformation tool

            Usage:
              turbophrase [options]              Start as system tray application
              turbophrase init [options]         Create default configuration file
              turbophrase config [options]       Show current configuration
              turbophrase test [name] [options]  Test provider connection
              turbophrase startup                Show startup registration status
              turbophrase startup --enable       Enable run at Windows startup
              turbophrase startup --disable      Disable run at Windows startup
              turbophrase settings               Open the Settings UI
              turbophrase setup                  Open provider setup wizard
              turbophrase secrets list           List secrets stored in Credential Manager
              turbophrase secrets set <name>     Save a secret (value read from stdin if omitted)
              turbophrase secrets get <name>     Print a stored secret
              turbophrase secrets remove <name>  Delete a stored secret
              turbophrase help                   Show this help message
              turbophrase version                Show version information

            Options:
              --config <path>  Use a custom configuration file path
              --init-config    Create the config file with defaults if it doesn't exist
                               (use with --config to specify the path)

            Configuration:
              Config file lookup order:
                1. --config <path>       (explicit path)
                2. <exe folder>\turbophrase.json  (portable mode, if present next to the .exe)
                3. <exe folder>\config.json       (portable mode, legacy)
                4. XDG_CONFIG_HOME/Turbophrase/turbophrase.json (preferred if it exists)
                5. XDG_CONFIG_HOME/Turbophrase/config.json      (legacy fallback)
                6. %APPDATA%\Turbophrase\turbophrase.json      (preferred default)
                7. %APPDATA%\Turbophrase\config.json           (legacy fallback)
              Supports environment variable substitution: ${OPENAI_API_KEY}

            Default hotkeys:
              Ctrl+Shift+G  Fix grammar
              Ctrl+Shift+P  Paraphrase text
              Ctrl+Shift+F  Make formal
              Ctrl+Shift+C  Make casual

            Notification settings (in turbophrase.json):
              notifications.showOnStartup           Show notification on app startup
              notifications.showOnSuccess           Show notification on successful transform
              notifications.showOnError             Show notification on errors
              notifications.showOnConfigReload      Show notification on config reload
              notifications.showOnProviderChange    Show notification on provider change
              notifications.showProcessingOverlay   Show processing overlay during transform
              notifications.showProcessingAnimation Animate tray icon during transform

            Logging settings (in turbophrase.json):
              logging.enabled                       Write diagnostic events to turbophrase.log (default: false)

            Crash diagnostics:
              Regardless of the logging.enabled setting, fatal startup errors
              are written to %TEMP%\Turbophrase-crash-*.log and a one-line
              summary is appended to %TEMP%\Turbophrase-bootstrap.log.
            """);
    }
}
