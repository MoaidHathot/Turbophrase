using Turbophrase;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;

/// <summary>
/// Turbophrase - AI-powered text transformation tool.
/// </summary>
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Enable high DPI support for proper icon scaling
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

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
                        
                        // Show MessageBox if running as GUI (no CLI commands)
                        if (remainingArgs.Count == 0)
                        {
                            ApplicationConfiguration.Initialize();
                            MessageBox.Show(errorMsg, "Turbophrase Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return 1;
                    }
                }
                else
                {
                    var errorMsg = $"Configuration file not found: {fullPath}\n\nUse --init-config to create it with default values.";
                    Console.Error.WriteLine(errorMsg);
                    
                    // Show MessageBox if running as GUI (no CLI commands)
                    if (remainingArgs.Count == 0)
                    {
                        ApplicationConfiguration.Initialize();
                        MessageBox.Show(errorMsg, "Turbophrase Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return 1;
                }
            }
            
            ConfigurationService.SetCustomConfigPath(fullPath);
        }

        // Handle CLI commands
        if (remainingArgs.Count > 0)
        {
            return HandleCliCommand(remainingArgs.ToArray());
        }

        // Run as tray application
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
        return 0;
    }

    private static int HandleCliCommand(string[] args)
    {
        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "init":
                return InitCommand();

            case "config":
                return ConfigCommand();

            case "test":
                return TestCommand(args.Length > 1 ? args[1] : null);

            case "startup":
                return StartupCommand(args.Skip(1).ToArray());

            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                return 0;

            case "version":
            case "--version":
            case "-v":
                Console.WriteLine("Turbophrase v1.0.2");
                return 0;

            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintHelp();
                return 1;
        }
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
                var status = hasKey || provider.Type == "copilot-cli" || provider.Type == "ollama"
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

    private static int TestCommand(string? providerName)
    {
        Console.WriteLine("Testing provider connection...");

        try
        {
            var config = ConfigurationService.LoadConfiguration();
            var orchestrator = new TextTransformOrchestrator(config);

            var targetProvider = providerName ?? config.DefaultProvider;

            Console.WriteLine($"Testing provider: {targetProvider}");

            var task = orchestrator.TestProviderAsync(targetProvider);
            task.Wait();
            var result = task.Result;

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
              turbophrase help                   Show this help message
              turbophrase version                Show version information

            Options:
              --config <path>  Use a custom configuration file path
              --init-config    Create the config file with defaults if it doesn't exist
                               (use with --config to specify the path)

            Configuration:
              Config file lookup order:
                1. --config <path>       (explicit path)
                2. XDG_CONFIG_HOME/Turbophrase/config.json (if env var set and file exists)
                3. %APPDATA%\Turbophrase\config.json       (default)
              Supports environment variable substitution: ${OPENAI_API_KEY}

            Default hotkeys:
              Ctrl+Shift+G  Fix grammar
              Ctrl+Shift+P  Paraphrase text
              Ctrl+Shift+F  Make formal
              Ctrl+Shift+C  Make casual

            Notification settings (in config.json):
              notifications.showOnStartup           Show notification on app startup
              notifications.showOnSuccess           Show notification on successful transform
              notifications.showOnError             Show notification on errors
              notifications.showOnConfigReload      Show notification on config reload
              notifications.showOnProviderChange    Show notification on provider change
              notifications.showProcessingOverlay   Show processing overlay during transform
              notifications.showProcessingAnimation Animate tray icon during transform
            """);
    }
}
