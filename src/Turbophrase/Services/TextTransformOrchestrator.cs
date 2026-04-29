using Microsoft.Toolkit.Uwp.Notifications;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Services;

/// <summary>
/// Orchestrates the text transformation flow: copy -> AI -> paste.
/// </summary>
public class TextTransformOrchestrator
{
    private readonly TurbophraseConfig _config;
    private readonly Dictionary<string, IAIProvider> _providers;
    private readonly ClipboardService _clipboardService;
    private bool _isProcessing;

    public TextTransformOrchestrator(TurbophraseConfig config)
    {
        _config = config;
        _providers = ProviderFactory.CreateProviders(config);
        _clipboardService = new ClipboardService();
    }

    /// <summary>
    /// Gets the list of available provider names.
    /// </summary>
    public IEnumerable<string> AvailableProviders => _providers.Keys;

    /// <summary>
    /// Gets the list of available preset names.
    /// </summary>
    public IEnumerable<string> AvailablePresets => _config.Presets.Keys;

    /// <summary>
    /// Captures the currently selected text and remembers the source window.
    /// </summary>
    public async Task<SelectionCaptureResult> CaptureSelectedTextAsync()
    {
        var sourceWindowHandle = _clipboardService.GetActiveWindowHandle();
        RuntimeLog.Write($"selection-capture-start hwnd=0x{sourceWindowHandle.ToInt64():X}");
        var selectedText = await _clipboardService.GetSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            RuntimeLog.Write("selection-capture-empty");
            return SelectionCaptureResult.Fail("No text is selected.");
        }

        RuntimeLog.Write($"selection-capture-success length={selectedText.Length}");
        return SelectionCaptureResult.Ok(selectedText, sourceWindowHandle);
    }

    /// <summary>
    /// Transforms the currently selected text using the specified preset.
    /// </summary>
    /// <param name="presetName">The name of the preset to use.</param>
    /// <returns>The transformation result.</returns>
    public async Task<TransformResult> TransformSelectedTextAsync(string presetName)
    {
        if (!_config.Presets.TryGetValue(presetName, out var preset))
        {
            RuntimeLog.Write($"preset-not-found preset='{presetName}'");
            return TransformResult.Fail($"Preset '{presetName}' not found.");
        }

        RuntimeLog.Write($"preset-transform-start preset='{presetName}' provider='{preset.Provider ?? _config.DefaultProvider}'");

        var captureResult = await CaptureSelectedTextAsync();
        if (!captureResult.Success)
        {
            return TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected.");
        }

        return await TransformCapturedTextAsync(captureResult, preset.SystemPrompt, preset.Provider, restoreFocusBeforePaste: false);
    }

    /// <summary>
    /// Transforms previously captured text using the specified preset.
    /// </summary>
    public Task<TransformResult> TransformCapturedTextWithPresetAsync(
        SelectionCaptureResult captureResult,
        string presetName,
        bool restoreFocusBeforePaste = true)
    {
        if (!captureResult.Success || string.IsNullOrWhiteSpace(captureResult.SelectedText))
        {
            RuntimeLog.Write($"captured-preset-transform-invalid-capture error='{captureResult.ErrorMessage}'");
            return Task.FromResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."));
        }

        if (!_config.Presets.TryGetValue(presetName, out var preset))
        {
            RuntimeLog.Write($"preset-not-found preset='{presetName}'");
            return Task.FromResult(TransformResult.Fail($"Preset '{presetName}' not found."));
        }

        RuntimeLog.Write($"captured-preset-transform-start preset='{presetName}' provider='{preset.Provider ?? _config.DefaultProvider}'");
        return TransformTextAsync(captureResult.SelectedText, preset, captureResult.SourceWindowHandle, restoreFocusBeforePaste);
    }

    /// <summary>
    /// Transforms previously captured text using a user-supplied prompt.
    /// </summary>
    public Task<TransformResult> TransformCapturedTextAsync(
        SelectionCaptureResult captureResult,
        string prompt,
        string? providerName = null,
        bool restoreFocusBeforePaste = true)
    {
        if (!captureResult.Success || string.IsNullOrWhiteSpace(captureResult.SelectedText))
        {
            RuntimeLog.Write($"custom-transform-invalid-capture error='{captureResult.ErrorMessage}'");
            return Task.FromResult(TransformResult.Fail(captureResult.ErrorMessage ?? "No text is selected."));
        }

        var preset = new PromptPreset
        {
            Name = "Custom Prompt",
            SystemPrompt = prompt,
            Provider = providerName
        };

        return TransformTextAsync(captureResult.SelectedText, preset, captureResult.SourceWindowHandle, restoreFocusBeforePaste);
    }

    /// <summary>
    /// Tests connection to a specific provider.
    /// </summary>
    public async Task<TransformResult> TestProviderAsync(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            return TransformResult.Fail($"Provider '{providerName}' not found.");
        }

        if (!provider.ValidateConfiguration())
        {
            return TransformResult.Fail($"Provider '{providerName}' is not properly configured.");
        }

        try
        {
            var result = await provider.TransformTextAsync(
                "Hello, this is a test message.",
                "Respond with 'Connection successful!' and nothing else.");
            return TransformResult.Ok(result, providerName);
        }
        catch (Exception ex)
        {
            return TransformResult.Fail($"Connection test failed: {ex.Message}", providerName);
        }
    }

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    public static void ShowErrorNotification(string title, string message)
    {
        ShowNotification(title, message, isError: true);
    }

    /// <summary>
    /// Shows a success notification.
    /// </summary>
    public static void ShowSuccessNotification(string presetName, string? providerName = null)
    {
        var message = providerName != null
            ? $"{presetName} completed using {providerName}"
            : $"{presetName} completed";

        ShowNotification("Turbophrase", message, isError: false);
    }

    /// <summary>
    /// Shows a notification with the specified title and message.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="isError">Whether this is an error notification.</param>
    public static void ShowNotification(string title, string message, bool isError)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);

            // Add app icon if available (use PNG, no circle crop for this icon design)
            var iconPath = GetAppIconPath();
            System.Diagnostics.Debug.WriteLine($"Toast icon path: {iconPath}");
            if (iconPath != null)
            {
                builder.AddAppLogoOverride(new Uri(iconPath), ToastGenericAppLogoCrop.None);
            }

            builder.Show();
        }
        catch
        {
            // Fallback: If toast notifications fail, we silently ignore
            // The user will still see the result of the operation
        }
    }

    /// <summary>
    /// Gets the path to the app icon for notifications.
    /// </summary>
    private static string? GetAppIconPath()
    {
        try
        {
            var appDir = AppContext.BaseDirectory;

            // Prefer dedicated toast PNG (64x64, optimized for notifications)
            var iconPath = Path.Combine(appDir, "Turbophrase_toast.png");
            if (File.Exists(iconPath))
            {
                return iconPath;
            }

            // Fallback to main PNG
            iconPath = Path.Combine(appDir, "Turbophrase.png");
            if (File.Exists(iconPath))
            {
                return iconPath;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private async Task<TransformResult> TransformTextAsync(
        string selectedText,
        PromptPreset preset,
        IntPtr sourceWindowHandle,
        bool restoreFocusBeforePaste)
    {
        if (_isProcessing)
        {
            RuntimeLog.Write("transform-rejected already-processing");
            return TransformResult.Fail("A transformation is already in progress.");
        }

        if (string.IsNullOrWhiteSpace(preset.SystemPrompt))
        {
            RuntimeLog.Write("transform-rejected empty-prompt");
            return TransformResult.Fail("Prompt cannot be empty.");
        }

        _isProcessing = true;

        try
        {
            IAIProvider provider;
            try
            {
                provider = ProviderFactory.GetProviderForPreset(preset, _config, _providers);
            }
            catch (InvalidOperationException ex)
            {
                RuntimeLog.Write($"provider-resolution-failed error='{ex.Message}'");
                return TransformResult.Fail(ex.Message);
            }

            if (!provider.ValidateConfiguration())
            {
                RuntimeLog.Write($"provider-invalid provider='{provider.Name}'");
                return TransformResult.Fail($"Provider '{provider.Name}' is not properly configured.");
            }

            RuntimeLog.Write($"provider-transform-start provider='{provider.Name}' textLength={selectedText.Length} restoreFocus={restoreFocusBeforePaste}");

            string transformedText;
            try
            {
                transformedText = await provider.TransformTextAsync(selectedText, preset.SystemPrompt);
            }
            catch (Exception ex)
            {
                RuntimeLog.Write($"provider-transform-failed provider='{provider.Name}' error='{ex.Message}'");
                return TransformResult.Fail($"AI transformation failed: {ex.Message}", provider.Name);
            }

            RuntimeLog.Write($"provider-transform-success provider='{provider.Name}' resultLength={transformedText.Length}");

            if (restoreFocusBeforePaste)
            {
                RuntimeLog.Write($"restore-focus hwnd=0x{sourceWindowHandle.ToInt64():X}");
                _clipboardService.RestoreWindowFocus(sourceWindowHandle);
                await Task.Delay(100);
            }

            RuntimeLog.Write("paste-start");
            await _clipboardService.ReplaceSelectedTextAsync(transformedText);
            RuntimeLog.Write("paste-success");

            return TransformResult.Ok(transformedText, provider.Name);
        }
        finally
        {
            _isProcessing = false;
        }
    }
}

/// <summary>
/// Result of capturing the user's current text selection.
/// </summary>
public sealed class SelectionCaptureResult
{
    private SelectionCaptureResult(bool success, string? selectedText, IntPtr sourceWindowHandle, string? errorMessage)
    {
        Success = success;
        SelectedText = selectedText;
        SourceWindowHandle = sourceWindowHandle;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? SelectedText { get; }

    public IntPtr SourceWindowHandle { get; }

    public string? ErrorMessage { get; }

    public static SelectionCaptureResult Ok(string selectedText, IntPtr sourceWindowHandle)
        => new(true, selectedText, sourceWindowHandle, null);

    public static SelectionCaptureResult Fail(string errorMessage)
        => new(false, null, IntPtr.Zero, errorMessage);
}
