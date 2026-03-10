using System.Reflection;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;

namespace Turbophrase;

/// <summary>
/// Application context for the system tray application.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private const int WM_HOTKEY = 0x0312;

    private readonly NotifyIcon _trayIcon;
    private TurbophraseConfig _config;
    private readonly GlobalHotkeyService _hotkeyService;
    private TextTransformOrchestrator _orchestrator;
    private readonly HiddenMessageWindow _messageWindow;
    private readonly ConfigurationWatcher _configWatcher;
    private readonly TrayIconAnimator _iconAnimator;
    private readonly ProcessingOverlay _processingOverlay;

    public TrayApplicationContext()
    {
        try
        {
            // Load configuration
            _config = ConfigurationService.LoadConfiguration();

            // Create hidden window for message handling
            _messageWindow = new HiddenMessageWindow(this);

            // Initialize services
            _hotkeyService = new GlobalHotkeyService(_messageWindow.Handle);
            _orchestrator = new TextTransformOrchestrator(_config);

            // Create tray icon with context menu
            _trayIcon = new NotifyIcon
            {
                Icon = LoadApplicationIcon(),
                Visible = true,
                Text = "Turbophrase - AI Text Transformer",
                ContextMenuStrip = CreateContextMenu()
            };

            // Create icon animator for processing indication
            _iconAnimator = new TrayIconAnimator(_trayIcon);

            // Create processing overlay for visible feedback
            _processingOverlay = new ProcessingOverlay();

            // Register hotkeys
            RegisterHotkeys();

            // Subscribe to hotkey events
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;

            // Start watching configuration file for changes
            _configWatcher = new ConfigurationWatcher();
            _configWatcher.ConfigurationChanged += OnConfigurationChanged;

            // Show startup notification (if enabled)
            if (_config.Notifications.ShowOnStartup)
            {
                _trayIcon.BalloonTipTitle = "Turbophrase";
                _trayIcon.BalloonTipText = "Running in system tray. Use hotkeys to transform text.";
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(3000);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start Turbophrase:\n\n{ex.Message}", "Turbophrase Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    /// <summary>
    /// Handles WM_HOTKEY messages from the hidden window.
    /// </summary>
    public void HandleHotkeyMessage(int hotkeyId)
    {
        _hotkeyService.HandleHotkeyMessage(hotkeyId);
    }

    private void RegisterHotkeys()
    {
        var registered = _hotkeyService.RegisterHotkeys(_config.Hotkeys);

        if (registered.Count == 0)
        {
            ShowNotification(
                "Turbophrase",
                "No hotkeys were registered. Check configuration.",
                isError: true);
        }
        else if (registered.Count < _config.Hotkeys.Count)
        {
            var failed = _config.Hotkeys.Count - registered.Count;
            ShowNotification(
                "Turbophrase",
                $"{failed} hotkey(s) failed to register. They may be in use by another application.",
                isError: true);
        }
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread since FileSystemWatcher events come from a thread pool thread
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.BeginInvoke(ReloadConfiguration);
        }
        else
        {
            ReloadConfiguration();
        }
    }

    private void ReloadConfiguration()
    {
        try
        {
            // Reload configuration
            var newConfig = ConfigurationService.LoadConfiguration();

            // Unregister old hotkeys
            _hotkeyService.UnregisterAll();

            // Update config and orchestrator
            _config = newConfig;
            _orchestrator = new TextTransformOrchestrator(_config);

            // Re-register hotkeys with new config
            RegisterHotkeys();

            // Update context menu
            _trayIcon.ContextMenuStrip = CreateContextMenu();

            // Notify user (if enabled)
            if (_config.Notifications.ShowOnConfigReload)
            {
                ShowNotification("Turbophrase", "Configuration reloaded", isError: false);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(
                "Configuration Reload Failed",
                ex.Message,
                isError: true);
        }
    }

    private void ChangeDefaultProvider(string providerName)
    {
        try
        {
            // Save the new default provider to config file
            ConfigurationService.SaveDefaultProvider(providerName);

            // Update in-memory config
            _config.DefaultProvider = providerName;
            _orchestrator = new TextTransformOrchestrator(_config);

            // Update context menu to reflect the change
            _trayIcon.ContextMenuStrip = CreateContextMenu();

            // Notify user (if enabled)
            if (_config.Notifications.ShowOnProviderChange)
            {
                ShowNotification("Turbophrase", $"Default provider changed to: {providerName}", isError: false);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(
                "Provider Change Failed",
                ex.Message,
                isError: true);
        }
    }

    private void ShowNotification(string title, string message, bool isError)
    {
        // Check notification settings
        if (isError && !_config.Notifications.ShowOnError)
            return;
        if (!isError && !_config.Notifications.ShowOnSuccess)
            return;

        TextTransformOrchestrator.ShowNotification(title, message, isError);
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        try
        {
            // Show processing indicators (if enabled)
            if (_config.Notifications.ShowProcessingAnimation)
                _iconAnimator.StartAnimation();
            if (_config.Notifications.ShowProcessingOverlay)
                _processingOverlay.ShowOverlay();

            var result = await _orchestrator.TransformSelectedTextAsync(e.Binding.Preset);

            // Hide processing indicators
            _iconAnimator.StopAnimation();
            _processingOverlay.HideOverlay();

            if (!result.Success)
            {
                if (_config.Notifications.ShowOnError)
                {
                    TextTransformOrchestrator.ShowNotification(
                        "Turbophrase Error",
                        result.ErrorMessage ?? "An unknown error occurred.",
                        isError: true);
                }
            }
            else
            {
                if (_config.Notifications.ShowOnSuccess)
                {
                    // Get the preset display name
                    var presetDisplayName = _config.Presets.TryGetValue(e.Binding.Preset, out var preset)
                        ? preset.Name ?? e.Binding.Preset
                        : e.Binding.Preset;
                    var message = result.ProviderName != null
                        ? $"{presetDisplayName} completed using {result.ProviderName}"
                        : $"{presetDisplayName} completed";
                    TextTransformOrchestrator.ShowNotification("Turbophrase", message, isError: false);
                }
            }
        }
        catch (Exception ex)
        {
            // Ensure indicators are hidden even on exception
            _iconAnimator.StopAnimation();
            _processingOverlay.HideOverlay();

            if (_config.Notifications.ShowOnError)
            {
                TextTransformOrchestrator.ShowNotification(
                    "Turbophrase Error",
                    ex.Message,
                    isError: true);
            }
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Presets submenu
        var presetsMenu = new ToolStripMenuItem("Transform");
        foreach (var (key, preset) in _config.Presets)
        {
            var presetName = key;
            var presetDisplayName = preset.Name ?? key;
            var item = new ToolStripMenuItem(presetDisplayName);
            item.Click += async (_, _) =>
            {
                if (_config.Notifications.ShowProcessingAnimation)
                    _iconAnimator.StartAnimation();
                if (_config.Notifications.ShowProcessingOverlay)
                    _processingOverlay.ShowOverlay();
                try
                {
                    var result = await _orchestrator.TransformSelectedTextAsync(presetName);
                    if (!result.Success)
                    {
                        if (_config.Notifications.ShowOnError)
                        {
                            TextTransformOrchestrator.ShowNotification(
                                "Turbophrase Error",
                                result.ErrorMessage ?? "An unknown error occurred.",
                                isError: true);
                        }
                    }
                    else
                    {
                        if (_config.Notifications.ShowOnSuccess)
                        {
                            var message = result.ProviderName != null
                                ? $"{presetDisplayName} completed using {result.ProviderName}"
                                : $"{presetDisplayName} completed";
                            TextTransformOrchestrator.ShowNotification("Turbophrase", message, isError: false);
                        }
                    }
                }
                finally
                {
                    _iconAnimator.StopAnimation();
                    _processingOverlay.HideOverlay();
                }
            };
            presetsMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(presetsMenu);

        menu.Items.Add(new ToolStripSeparator());

        // Hotkeys info
        var hotkeysMenu = new ToolStripMenuItem("Hotkeys");
        foreach (var hotkey in _config.Hotkeys)
        {
            var presetName = _config.Presets.TryGetValue(hotkey.Preset, out var p)
                ? p.Name ?? hotkey.Preset
                : hotkey.Preset;
            hotkeysMenu.DropDownItems.Add(new ToolStripMenuItem($"{hotkey.Keys} - {presetName}")
            {
                Enabled = false
            });
        }
        menu.Items.Add(hotkeysMenu);

        // Providers - now with click handlers for switching
        var providersMenu = new ToolStripMenuItem("Providers");
        foreach (var providerName in _orchestrator.AvailableProviders)
        {
            var isDefault = providerName == _config.DefaultProvider;
            var providerItem = new ToolStripMenuItem(providerName)
            {
                Checked = isDefault,
                CheckOnClick = false
            };
            var capturedProviderName = providerName; // Capture for closure
            providerItem.Click += (_, _) =>
            {
                if (capturedProviderName != _config.DefaultProvider)
                {
                    ChangeDefaultProvider(capturedProviderName);
                }
            };
            providersMenu.DropDownItems.Add(providerItem);
        }
        menu.Items.Add(providersMenu);

        menu.Items.Add(new ToolStripSeparator());

        // Open config folder
        var configItem = new ToolStripMenuItem("Open Config Folder");
        configItem.Click += (_, _) =>
        {
            if (Directory.Exists(ConfigurationService.ConfigDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", ConfigurationService.ConfigDirectory);
            }
            else
            {
                if (_config.Notifications.ShowOnError)
                {
                    TextTransformOrchestrator.ShowNotification(
                        "Turbophrase",
                        "Config folder does not exist. Run 'turbophrase init' first.",
                        isError: true);
                }
            }
        };
        menu.Items.Add(configItem);

        // Reload config
        var reloadItem = new ToolStripMenuItem("Reload Configuration");
        reloadItem.Click += (_, _) =>
        {
            ReloadConfiguration();
        };
        menu.Items.Add(reloadItem);

        // Run at startup toggle
        var startupItem = new ToolStripMenuItem("Run at Windows startup")
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = false
        };
        startupItem.Click += (_, _) =>
        {
            try
            {
                if (StartupManager.IsEnabled())
                {
                    StartupManager.Disable();
                    startupItem.Checked = false;
                    if (_config.Notifications.ShowOnSuccess)
                        TextTransformOrchestrator.ShowNotification("Turbophrase",
                            "Removed from Windows startup", isError: false);
                }
                else
                {
                    StartupManager.Enable(ConfigurationService.CustomConfigFilePath);
                    startupItem.Checked = true;
                    if (_config.Notifications.ShowOnSuccess)
                        TextTransformOrchestrator.ShowNotification("Turbophrase",
                            "Added to Windows startup", isError: false);
                }
            }
            catch (Exception ex)
            {
                if (_config.Notifications.ShowOnError)
                    TextTransformOrchestrator.ShowNotification("Startup Error",
                        ex.Message, isError: true);
            }
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            ExitThread();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Icon CreateTrayIcon()
    {
        // Create a simple tray icon programmatically
        // Using a 16x16 bitmap with a "T" character
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(0, 120, 215)); // Windows blue
            using var font = new Font("Segoe UI", 10, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            var textSize = g.MeasureString("T", font);
            var x = (16 - textSize.Width) / 2;
            var y = (16 - textSize.Height) / 2;
            g.DrawString("T", font, brush, x, y);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            // Load ICO from file system (not embedded) for best quality
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                var dir = Path.GetDirectoryName(exePath);
                var icoPath = Path.Combine(dir!, "Turbophrase.ico");
                if (File.Exists(icoPath))
                {
                    // Load directly from file - preserves all icon sizes
                    return new Icon(icoPath);
                }
            }
        }
        catch
        {
            // Fall through
        }

        try
        {
            // Try to load embedded ICO resource
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("Turbophrase.Resources.Turbophrase.ico");
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
            // Fall through to PNG fallback
        }

        try
        {
            // Fallback: Try PNG resource
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Turbophrase.Resources.Turbophrase.png");
            if (stream != null)
            {
                using var bitmap = new Bitmap(stream);
                using var resized = new Bitmap(bitmap, new Size(32, 32));
                return Icon.FromHandle(resized.GetHicon());
            }
        }
        catch
        {
            // Fall through to fallback
        }

        // Fallback to programmatically created icon
        return CreateTrayIcon();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _processingOverlay.Dispose();
            _iconAnimator.Dispose();
            _configWatcher.Dispose();
            _hotkeyService.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _messageWindow.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        base.ExitThreadCore();
    }
}

/// <summary>
/// Hidden window for receiving WM_HOTKEY messages.
/// </summary>
internal class HiddenMessageWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly TrayApplicationContext _context;

    public HiddenMessageWindow(TrayApplicationContext context)
    {
        _context = context;
        CreateHandle(new CreateParams
        {
            Caption = "TurbophraseMessageWindow",
            Style = 0
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            var hotkeyId = m.WParam.ToInt32();
            _context.HandleHotkeyMessage(hotkeyId);
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
