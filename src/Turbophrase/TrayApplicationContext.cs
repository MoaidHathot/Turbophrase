using System.Reflection;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;
using Turbophrase.Avalonia;
using Turbophrase.Avalonia.Windows;
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
    private readonly HotkeyMessageFilter _messageFilter;
    private readonly ConfigurationWatcher _configWatcher;
    private readonly TrayIconAnimator _iconAnimator;
    private readonly ProcessingOverlayWindow _processingOverlay;
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;
    private SettingsWindow? _settingsWindow;

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
            if (FirstRunWindow.ShouldShowFor(_config))
            {
                if (FirstRunWindow.ShowProviderSetupAsync().GetAwaiter().GetResult())
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
            _ = Task.Run(AvaloniaUiHost.EnsureInitialized);

            // Create tray icon with context menu
            _trayIcon = new NotifyIcon
            {
                Icon = LoadApplicationIcon(),
                Visible = true,
                Text = "Turbophrase - AI Text Transformer"
            };
            _trayIcon.MouseUp += OnTrayIconMouseUp;

            // Create icon animator for processing indication
            _iconAnimator = new TrayIconAnimator(_trayIcon);

            // Create processing overlay for visible feedback
            _processingOverlay = AvaloniaUiHost.Invoke(() => new ProcessingOverlayWindow());

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
            ShowStartupError(ex);
            throw;
        }
    }

    private static void ShowStartupError(Exception ex)
    {
        try
        {
            AvaloniaUiHost.ShowStandaloneWindowAsync(() => new AppMessageWindow(
                "Turbophrase Error",
                $"Failed to start Turbophrase:\n\n{ex.Message}",
                isError: true)).GetAwaiter().GetResult();
        }
        catch
        {
            // If Avalonia itself failed, write to stderr as the last available fallback.
            Console.Error.WriteLine($"Failed to start Turbophrase: {ex}");
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
        AvaloniaUiHost.Invoke(() =>
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }

    public async Task OpenProviderSetupWindowAsync()
    {
        if (await FirstRunWindow.ShowProviderSetupAsync())
        {
            ReloadConfiguration();
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
            AvaloniaUiHost.Invoke(_processingOverlay.HideOverlay);

            if (_config.Notifications.ShowOnError)
            {
                ShowNotification(
                    "Turbophrase Error",
                    ex.Message,
                    isError: true);
            }
        }
    }

    private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
        {
            OpenTrayMenuWindow();
        }
    }

    private void OpenTrayMenuWindow()
    {
        AvaloniaUiHost.Invoke(() =>
        {
            var menu = new TrayMenuWindow(BuildTrayMenuSections());
            menu.Show();
            menu.Activate();
        });
    }

    private IReadOnlyList<TrayMenuSection> BuildTrayMenuSections()
    {
        var transformItems = new List<TrayMenuItem>
        {
            new("Custom Prompt", "Capture selected text and enter one-off instructions.", InvokeAsync: () => RunOnTrayThread(() => ExecuteCustomPromptAsync()))
        };

        foreach (var (key, preset) in _config.Presets)
        {
            var presetName = key;
            var displayName = preset.Name ?? key;
            transformItems.Add(new TrayMenuItem(
                displayName,
                preset.Provider == null ? "Uses default provider" : $"Uses {preset.Provider}",
                InvokeAsync: () => RunOnTrayThread(() => ExecutePresetAsync(presetName, displayName))));
        }

        var hotkeyItems = _config.Hotkeys
            .Select(hotkey => new TrayMenuItem(GetBindingDisplayName(hotkey), hotkey.Keys, Enabled: false))
            .ToList();

        var providerItems = _orchestrator.AvailableProviders
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(providerName => new TrayMenuItem(
                providerName,
                providerName == _config.DefaultProvider ? "Default provider" : "Set as default",
                Checked: providerName == _config.DefaultProvider,
                InvokeAsync: providerName == _config.DefaultProvider
                    ? null
                    : () => RunOnTrayThread(() => ChangeDefaultProvider(providerName))))
            .ToList();

        return
        [
            new TrayMenuSection("Transform", transformItems),
            new TrayMenuSection("Hotkeys", hotkeyItems.Count == 0 ? [new TrayMenuItem("No hotkeys configured", Enabled: false)] : hotkeyItems),
            new TrayMenuSection("Providers", providerItems.Count == 0 ? [new TrayMenuItem("No providers available", Enabled: false)] : providerItems),
            new TrayMenuSection("App", BuildAppMenuItems())
        ];
    }

    private IReadOnlyList<TrayMenuItem> BuildAppMenuItems()
    {
        var startupEnabled = StartupManager.IsEnabled();
        return
        [
            new("Settings", "Configure providers, presets, hotkeys, and behavior.", InvokeAsync: () => RunOnTrayThread(OpenSettingsWindow)),
            new("Provider Setup", "Reopen the first-run provider and credential setup.", InvokeAsync: () => RunOnTrayThread(OpenProviderSetupWindowAsync)),
            new("Open Config Folder", ConfigurationService.ConfigDirectory, InvokeAsync: () => RunOnTrayThread(OpenConfigFolder)),
            new("Reload Configuration", ConfigurationService.ConfigFilePath, InvokeAsync: () => RunOnTrayThread(ReloadConfiguration)),
            new("Run at Windows startup", startupEnabled ? "Enabled" : "Disabled", Checked: startupEnabled, InvokeAsync: () => RunOnTrayThread(ToggleStartup)),
            new("Exit", "Close Turbophrase.", InvokeAsync: () => RunOnTrayThread(ExitThread))
        ];
    }

    private Task RunOnTrayThread(Action action) => RunOnTrayThread(() =>
    {
        action();
        return Task.CompletedTask;
    });

    private Task RunOnTrayThread(Func<Task> action)
    {
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            return action();
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(async _ =>
        {
            try
            {
                await action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                RuntimeLog.Write($"tray-menu-action-failed error='{ex.Message}'");
                ShowNotification("Turbophrase Error", ex.Message, isError: true);
                completion.TrySetResult();
            }
        }, null);

        return completion.Task;
    }

    private void OpenConfigFolder()
    {
        if (Directory.Exists(ConfigurationService.ConfigDirectory))
        {
            System.Diagnostics.Process.Start("explorer.exe", ConfigurationService.ConfigDirectory);
            return;
        }

        ShowNotification("Turbophrase", "Config folder does not exist. Run 'turbophrase init' first.", isError: true);
    }

    private void ToggleStartup()
    {
        try
        {
            if (StartupManager.IsEnabled())
            {
                StartupManager.Disable();
                if (_config.Notifications.ShowOnSuccess)
                {
                    TextTransformOrchestrator.ShowNotification("Turbophrase", "Removed from Windows startup", isError: false);
                }
            }
            else
            {
                StartupManager.Enable(ConfigurationService.CustomConfigFilePath);
                if (_config.Notifications.ShowOnSuccess)
                {
                    TextTransformOrchestrator.ShowNotification("Turbophrase", "Added to Windows startup", isError: false);
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Startup Error", ex.Message, isError: true);
        }
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
        var sourceWindowHandle = _orchestrator.GetActiveWindowHandle();
        var dialogTask = AvaloniaUiHost.InvokeAsync(() =>
        {
            var created = new CommandPaletteWindow(GetPickerOperations())
            {
                ShowActivated = false
            };
            created.SetCapturePending();
            return created;
        });

        var captureTask = CaptureForCommandSurfaceAsync(sourceWindowHandle);
        var dialog = await dialogTask;
        var captureResult = await captureTask;

        AvaloniaUiHost.Invoke(() =>
        {
            if (captureResult.Success)
            {
                dialog.SetCaptureReady();
            }
            else
            {
                dialog.SetCaptureFailed(captureResult.ErrorMessage ?? "No text is selected.");
            }

            dialog.ActivateForInput();
        });

        var windowClosed = AvaloniaUiHost.ShowWindowAsync(dialog);
        await windowClosed;

        var selectedOperation = dialog?.AcceptedOperation;
        if (dialog?.Accepted != true || selectedOperation == null)
        {
            return;
        }

        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        await ExecutePickedOperationAsync(selectedOperation, captureResult);
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
        var sourceWindowHandle = _orchestrator.GetActiveWindowHandle();
        var dialogTask = AvaloniaUiHost.InvokeAsync(() =>
        {
            var created = new PromptCommandWindow(_orchestrator.AvailableProviders, _config.DefaultProvider)
            {
                ShowActivated = false
            };
            created.SetCapturePending();
            return created;
        });

        var captureTask = CaptureForCommandSurfaceAsync(sourceWindowHandle);
        var dialog = await dialogTask;
        var captureResult = await captureTask;

        AvaloniaUiHost.Invoke(() =>
        {
            if (captureResult.Success)
            {
                dialog.SetCaptureReady();
            }
            else
            {
                dialog.SetCaptureFailed(captureResult.ErrorMessage ?? "No text is selected.");
            }

            dialog.ActivateForInput();
        });

        var windowClosed = AvaloniaUiHost.ShowWindowAsync(dialog);
        await windowClosed;

        if (dialog?.Accepted != true)
        {
            return;
        }

        var promptText = dialog.PromptText;
        var selectedProvider = dialog.SelectedProvider;
        if (string.IsNullOrWhiteSpace(promptText))
        {
            ShowTransformResult(TransformResult.Fail("Prompt cannot be empty."), GetBindingDisplayName(binding));
            return;
        }

        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        await ExecuteCustomPromptAsync(binding, captureResult, promptText, selectedProvider);
    }

    private async Task ExecuteCustomPromptAsync(HotkeyBinding? binding, SelectionCaptureResult captureResult)
    {
        PromptCommandWindow? dialog = null;
        AvaloniaUiHost.Invoke(() =>
        {
            dialog = new PromptCommandWindow(_orchestrator.AvailableProviders, _config.DefaultProvider);
            dialog.SetCaptureReady();
            dialog.ActivateForInput();
        });

        if (dialog == null)
        {
            return;
        }

        await AvaloniaUiHost.ShowWindowAsync(dialog);

        if (dialog?.Accepted != true)
        {
            return;
        }

        var promptText = dialog.PromptText;
        var selectedProvider = dialog.SelectedProvider;
        if (string.IsNullOrWhiteSpace(promptText))
        {
            ShowTransformResult(TransformResult.Fail("Prompt cannot be empty."), GetBindingDisplayName(binding));
            return;
        }

        await ExecuteCustomPromptAsync(binding, captureResult, promptText, selectedProvider);
    }

    private async Task ExecuteCustomPromptAsync(HotkeyBinding? binding, SelectionCaptureResult captureResult, string promptText, string? selectedProvider)
    {
        if (!captureResult.Success)
        {
            ShowTransformResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."), GetBindingDisplayName(binding));
            return;
        }

        await ExecuteTransformWithIndicatorsAsync(
            async () => await _orchestrator.TransformCapturedTextAsync(
                captureResult,
                BuildCustomPromptSystemPrompt(binding, promptText, captureResult.SelectedText ?? string.Empty),
                binding?.Provider ?? selectedProvider),
            GetBindingDisplayName(binding));
    }

    private async Task<SelectionCaptureResult> CaptureForCommandSurfaceAsync(IntPtr sourceWindowHandle)
    {
        try
        {
            var result = await _orchestrator.CaptureSelectedTextAsync(sourceWindowHandle, restoreFocusBeforeCopy: true);
            return result;
        }
        catch (Exception ex)
        {
            return SelectionCaptureResult.Fail($"Could not capture selected text: {ex.Message}");
        }
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
            AvaloniaUiHost.Invoke(_processingOverlay.ShowOverlay);

        try
        {
            var result = await operation();
            RuntimeLog.Write($"transform-operation-complete success={result.Success} provider='{result.ProviderName}' error='{result.ErrorMessage}'");
            ShowTransformResult(result, displayName);
        }
        finally
        {
            _iconAnimator.StopAnimation();
            AvaloniaUiHost.Invoke(_processingOverlay.HideOverlay);
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
            if (_settingsWindow != null)
            {
                AvaloniaUiHost.Invoke(() =>
                {
                    _settingsWindow.Close();
                    _settingsWindow = null;
                });
            }
            AvaloniaUiHost.Invoke(_processingOverlay.Close);
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
