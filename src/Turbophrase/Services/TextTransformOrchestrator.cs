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
    /// Transforms the currently selected text using the specified preset.
    /// </summary>
    /// <param name="presetName">The name of the preset to use.</param>
    /// <returns>The transformation result.</returns>
    public async Task<TransformResult> TransformSelectedTextAsync(string presetName)
    {
        if (_isProcessing)
        {
            return TransformResult.Fail("A transformation is already in progress.");
        }

        _isProcessing = true;

        try
        {
            // Get the preset
            if (!_config.Presets.TryGetValue(presetName, out var preset))
            {
                return TransformResult.Fail($"Preset '{presetName}' not found.");
            }

            // Get the provider for this preset
            IAIProvider provider;
            try
            {
                provider = ProviderFactory.GetProviderForPreset(preset, _config, _providers);
            }
            catch (InvalidOperationException ex)
            {
                return TransformResult.Fail(ex.Message);
            }

            // Validate provider configuration
            if (!provider.ValidateConfiguration())
            {
                return TransformResult.Fail($"Provider '{provider.Name}' is not properly configured.");
            }

            // Get selected text
            var selectedText = await _clipboardService.GetSelectedTextAsync();
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                return TransformResult.Fail("No text is selected.");
            }

            // Transform the text
            string transformedText;
            try
            {
                transformedText = await provider.TransformTextAsync(selectedText, preset.SystemPrompt);
            }
            catch (Exception ex)
            {
                return TransformResult.Fail($"AI transformation failed: {ex.Message}", provider.Name);
            }

            // Replace the selected text
            await _clipboardService.ReplaceSelectedTextAsync(transformedText);

            return TransformResult.Ok(transformedText, provider.Name);
        }
        finally
        {
            _isProcessing = false;
        }
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
}
