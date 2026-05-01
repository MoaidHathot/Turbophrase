using System.Reflection;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;
using Turbophrase.Services;
using Turbophrase.Settings;

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
    private readonly HotkeyMessageFilter _messageFilter;
    private readonly ConfigurationWatcher _configWatcher;
    private readonly TrayIconAnimator _iconAnimator;
    private readonly ProcessingOverlay _processingOverlay;
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        try
        {
            // Capture the UI thread's synchronization context so configuration reloads
            // (raised on a thread-pool thread by FileSystemWatcher) can be marshaled back
            // to the same thread that registered the global hotkeys. RegisterHotKey/
            // UnregisterHotKey have thread affinity when called with hWnd=NULL, so we must
            // run them on this thread or the hotkeys will leak and fail to re-register.
            if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            }
            _uiContext = SynchronizationContext.Current!;
            _uiThreadId = Environment.CurrentManagedThreadId;

            // Load configuration
            _config = ConfigurationService.LoadConfiguration();
            RuntimeLog.Configure(_config.Logging);
            RuntimeLog.Write("app-start");
            RuntimeLog.Write($"config-loaded path='{ConfigurationService.ConfigFilePath}' hotkeys={_config.Hotkeys.Count} defaultProvider='{_config.DefaultProvider}' logging={_config.Logging.Enabled}");

            // First-run onboarding: if no provider has usable credentials,
            // show the wizard before bringing up the rest of the tray. The
            // wizard writes turbophrase.json directly; we then re-load.
            if (FirstRunWizard.ShouldShowFor(_config))
            {
                using var wizard = new FirstRunWizard();
                if (wizard.ShowDialog() == DialogResult.OK)
                {
                    _config = ConfigurationService.LoadConfiguration();
                    RuntimeLog.Configure(_config.Logging);
                    RuntimeLog.Write("first-run-wizard-finished");
                }
            }

            // Initialize services
            _hotkeyService = new GlobalHotkeyService(IntPtr.Zero);
            _orchestrator = new TextTransformOrchestrator(_config);
            _messageFilter = new HotkeyMessageFilter(this);
            Application.AddMessageFilter(_messageFilter);

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
        RuntimeLog.Write($"hotkeys-register-summary registered={registered.Count} total={_config.Hotkeys.Count}");

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
        // FileSystemWatcher / debounce timer events arrive on a thread-pool thread.
        // RegisterHotKey/UnregisterHotKey have thread affinity (hotkeys registered
        // on the UI thread cannot be unregistered or re-registered from another
        // thread), so always marshal the reload back to the captured UI context.
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            ReloadConfiguration();
        }
        else
        {
            _uiContext.Post(_ => ReloadConfiguration(), null);
        }
    }

    private void ReloadConfiguration()
    {
        try
        {
            // Reload configuration
            var newConfig = ConfigurationService.LoadConfiguration();
            RuntimeLog.Configure(newConfig.Logging);

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

    /// <summary>
    /// Opens the Settings window. If one is already open, brings it to the
    /// foreground instead of creating a second instance. The window is
    /// non-modal and shares the underlying turbophrase.json with the tray:
    /// saves trigger the existing ConfigurationWatcher and hot-reload path,
    /// so no in-memory state is duplicated.
    /// </summary>
    public void OpenSettingsWindow()
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
            {
                _settingsForm.WindowState = FormWindowState.Normal;
            }

            _settingsForm.Activate();
            _settingsForm.BringToFront();
            return;
        }

        _settingsForm = new SettingsForm();
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
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

        // Toast notifications can fail silently on some systems, so show a tray balloon for errors too.
        if (isError)
        {
            try
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = message;
                _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                _trayIcon.ShowBalloonTip(4000);
            }
            catch
            {
                // Ignore balloon failures too.
            }
        }
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        try
        {
            RuntimeLog.Write($"hotkey-handler-start keys='{e.Binding.Keys}' action='{e.Binding.Action ?? "preset"}' preset='{e.Binding.Preset}'");
            await ExecuteBindingAsync(e.Binding);
            RuntimeLog.Write($"hotkey-handler-end keys='{e.Binding.Keys}'");
        }
        catch (Exception ex)
        {
            RuntimeLog.Write($"hotkey-handler-exception error='{ex.Message}'");
            // Ensure indicators are hidden even on exception
            _iconAnimator.StopAnimation();
            _processingOverlay.HideOverlay();

            if (_config.Notifications.ShowOnError)
            {
                ShowNotification(
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
        var customPromptItem = new ToolStripMenuItem("Custom Prompt...");
        customPromptItem.Click += async (_, _) =>
        {
            await ExecuteCustomPromptAsync();
        };
        presetsMenu.DropDownItems.Add(customPromptItem);
        presetsMenu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (key, preset) in _config.Presets)
        {
            var presetName = key;
            var presetDisplayName = preset.Name ?? key;
            var item = new ToolStripMenuItem(presetDisplayName);
            item.Click += async (_, _) =>
            {
                await ExecutePresetAsync(presetName, presetDisplayName);
            };
            presetsMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(presetsMenu);

        menu.Items.Add(new ToolStripSeparator());

        // Hotkeys info
        var hotkeysMenu = new ToolStripMenuItem("Hotkeys");
        foreach (var hotkey in _config.Hotkeys)
        {
            hotkeysMenu.DropDownItems.Add(new ToolStripMenuItem($"{hotkey.Keys} - {GetBindingDisplayName(hotkey)}")
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

        // Settings UI (lazy)
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettingsWindow();
        menu.Items.Add(settingsItem);

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

    private async Task ExecuteBindingAsync(HotkeyBinding binding)
    {
        if (binding.IsCustomPromptAction)
        {
            await ExecuteCustomPromptAsync(binding);
            return;
        }

        if (binding.IsPresetPickerAction)
        {
            await ExecutePresetPickerAsync(binding);
            return;
        }

        if (binding.IsPresetAction)
        {
            await ExecutePresetAsync(binding.Preset, GetBindingDisplayName(binding));
            return;
        }

        ShowNotification("Turbophrase", $"Unsupported action '{binding.Action}'.", isError: true);
    }

    private async Task ExecutePresetAsync(string presetName, string displayName)
    {
        await ExecuteTransformWithIndicatorsAsync(
            async () => await _orchestrator.TransformSelectedTextAsync(presetName),
            displayName);
    }

    private async Task ExecutePresetPickerAsync(HotkeyBinding binding)
    {
        var captureResult = await _orchestrator.CaptureSelectedTextAsync();
        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        using var dialog = new PresetPickerDialog(GetPickerOperations());
        if (dialog.ShowDialog() != DialogResult.OK || dialog.SelectedOperation == null)
        {
            return;
        }

        await ExecutePickedOperationAsync(dialog.SelectedOperation, captureResult);
    }

    private async Task ExecutePickedOperationAsync(PickerOperation operation, SelectionCaptureResult captureResult)
    {
        var binding = operation.Binding;
        if (binding.IsCustomPromptAction)
        {
            await ExecuteCustomPromptAsync(binding, captureResult);
            return;
        }

        if (binding.IsPresetAction)
        {
            await ExecuteTransformWithIndicatorsAsync(
                async () => await _orchestrator.TransformCapturedTextWithPresetAsync(captureResult, binding.Preset),
                operation.DisplayName);
            return;
        }

        ShowNotification("Turbophrase", $"Unsupported picker action '{binding.Action}'.", isError: true);
    }

    private async Task ExecuteCustomPromptAsync(HotkeyBinding? binding = null)
    {
        var captureResult = await _orchestrator.CaptureSelectedTextAsync();
        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        await ExecuteCustomPromptAsync(binding, captureResult);
    }

    private async Task ExecuteCustomPromptAsync(HotkeyBinding? binding, SelectionCaptureResult captureResult)
    {
        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        using var dialog = new CustomPromptDialog(_orchestrator.AvailableProviders, _config.DefaultProvider);
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(dialog.PromptText))
        {
            ShowTransformResult(TransformResult.Fail("Prompt cannot be empty."), GetBindingDisplayName(binding));
            return;
        }

        await ExecuteTransformWithIndicatorsAsync(
            async () => await _orchestrator.TransformCapturedTextAsync(
                captureResult,
                BuildCustomPromptSystemPrompt(binding, dialog.PromptText, captureResult.SelectedText ?? string.Empty),
                binding?.Provider ?? dialog.SelectedProvider),
            GetBindingDisplayName(binding));
    }

    private List<PickerOperation> GetPickerOperations()
    {
        var operations = new List<(int? Order, int Sequence, PickerOperation Operation)>();
        var sequence = 0;

        foreach (var (presetName, preset) in _config.Presets)
        {
            if (!preset.IncludeInPicker)
            {
                continue;
            }

            var binding = new HotkeyBinding { Preset = presetName };
            var displayName = preset.Name ?? presetName;
            operations.Add((preset.PickerOrder, sequence++, new PickerOperation(presetName, displayName, binding)));
        }

        foreach (var action in _config.PickerActions.Concat(_config.Hotkeys.Where(binding => binding.IncludeInPicker)))
        {
            if (!action.IncludeInPicker)
            {
                continue;
            }

            operations.Add((action.PickerOrder, sequence++, new PickerOperation(GetPickerActionId(action), GetBindingDisplayName(action), action)));
        }

        return operations
            .OrderBy(item => item.Order ?? int.MaxValue)
            .ThenBy(item => item.Sequence)
            .Select(item => item.Operation)
            .ToList();
    }

    private static string GetPickerActionId(HotkeyBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.Name))
        {
            return binding.Name;
        }

        if (!string.IsNullOrWhiteSpace(binding.Action))
        {
            return binding.Action;
        }

        return binding.Preset;
    }

    private async Task ExecuteTransformWithIndicatorsAsync(Func<Task<TransformResult>> operation, string displayName)
    {
        RuntimeLog.Write($"transform-indicators-start display='{displayName}' overlay={_config.Notifications.ShowProcessingOverlay} animation={_config.Notifications.ShowProcessingAnimation}");
        if (_config.Notifications.ShowProcessingAnimation)
            _iconAnimator.StartAnimation();
        if (_config.Notifications.ShowProcessingOverlay)
            _processingOverlay.ShowOverlay();

        try
        {
            var result = await operation();
            RuntimeLog.Write($"transform-operation-complete success={result.Success} provider='{result.ProviderName}' error='{result.ErrorMessage}'");
            ShowTransformResult(result, displayName);
        }
        finally
        {
            _iconAnimator.StopAnimation();
            _processingOverlay.HideOverlay();
            RuntimeLog.Write("transform-indicators-stop");
        }
    }

    private void ShowTransformResult(TransformResult result, string displayName)
    {
        if (!result.Success)
        {
            if (_config.Notifications.ShowOnError)
            {
                ShowNotification(
                    "Turbophrase Error",
                    result.ErrorMessage ?? "An unknown error occurred.",
                    isError: true);
            }

            return;
        }

        if (_config.Notifications.ShowOnSuccess)
        {
            var message = result.ProviderName != null
                ? $"{displayName} completed using {result.ProviderName}"
                : $"{displayName} completed";
            TextTransformOrchestrator.ShowNotification("Turbophrase", message, isError: false);
        }
    }

    private string GetBindingDisplayName(HotkeyBinding? binding)
    {
        if (binding == null)
        {
            return "Custom Prompt";
        }

        if (binding.IsCustomPromptAction)
        {
            return !string.IsNullOrWhiteSpace(binding.Name) ? binding.Name : "Custom Prompt";
        }

        if (binding.IsPresetPickerAction)
        {
            return !string.IsNullOrWhiteSpace(binding.Name) ? binding.Name : "Choose Operation";
        }

        return GetPresetDisplayName(binding.Preset);
    }

    private string GetPresetDisplayName(string presetName)
    {
        return _config.Presets.TryGetValue(presetName, out var preset)
            ? preset.Name ?? presetName
            : presetName;
    }

    private string BuildCustomPromptSystemPrompt(HotkeyBinding? binding, string instruction, string selectedText)
    {
        var template = binding?.SystemPromptTemplate ?? _config.CustomPrompt.SystemPromptTemplate;
        return template
            .Replace("{instruction}", instruction, StringComparison.Ordinal)
            .Replace("{text}", selectedText, StringComparison.Ordinal);
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
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Close();
                _settingsForm.Dispose();
                _settingsForm = null;
            }
            _processingOverlay.Dispose();
            _iconAnimator.Dispose();
            _configWatcher.Dispose();
            _hotkeyService.Dispose();
            Application.RemoveMessageFilter(_messageFilter);
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
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
/// Message filter for receiving WM_HOTKEY messages from the UI thread queue.
/// </summary>
internal sealed class HotkeyMessageFilter : IMessageFilter
{
    private const int WM_HOTKEY = 0x0312;
    private readonly TrayApplicationContext _context;

    public HotkeyMessageFilter(TrayApplicationContext context)
    {
        _context = context;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            var hotkeyId = m.WParam.ToInt32();
            _context.HandleHotkeyMessage(hotkeyId);
            return true;
        }

        return false;
    }
}
