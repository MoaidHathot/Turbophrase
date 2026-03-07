using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Abstractions;

/// <summary>
/// Interface for AI providers that can transform text.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// The name/identifier of this provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Transforms text using the AI model.
    /// </summary>
    /// <param name="text">The text to transform.</param>
    /// <param name="systemPrompt">The system prompt defining the transformation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed text.</returns>
    Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the provider is properly configured.
    /// </summary>
    /// <returns>True if configured correctly, false otherwise.</returns>
    bool ValidateConfiguration();
}

/// <summary>
/// Base class for AI providers with common functionality.
/// </summary>
public abstract class AIProviderBase : IAIProvider
{
    protected readonly ProviderConfig Config;

    protected AIProviderBase(string name, ProviderConfig config)
    {
        Name = name;
        Config = config;
    }

    public string Name { get; }

    public abstract Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default);

    public abstract bool ValidateConfiguration();

    /// <summary>
    /// Gets the model name from config or returns the default.
    /// </summary>
    protected string GetModelOrDefault(string defaultModel)
        => !string.IsNullOrEmpty(Config.Model) ? Config.Model : defaultModel;

    /// <summary>
    /// Gets the max tokens from config or returns the default.
    /// </summary>
    protected int GetMaxTokensOrDefault(int defaultTokens)
        => Config.MaxTokens ?? defaultTokens;

    /// <summary>
    /// Gets the temperature from config or returns the default.
    /// </summary>
    protected float GetTemperatureOrDefault(float defaultTemp)
        => Config.Temperature.HasValue ? (float)Config.Temperature.Value : defaultTemp;
}
