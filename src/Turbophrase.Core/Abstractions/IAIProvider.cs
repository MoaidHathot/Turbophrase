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
    /// Transforms text using the AI model, with optional per-call overrides
    /// (reasoning effort, etc.). Providers that do not support a given
    /// option silently ignore it.
    /// </summary>
    /// <param name="text">The text to transform.</param>
    /// <param name="systemPrompt">The system prompt defining the transformation.</param>
    /// <param name="options">Per-call options resolved from the active preset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed text.</returns>
    Task<string> TransformTextAsync(
        string text,
        string systemPrompt,
        TransformOptions? options,
        CancellationToken cancellationToken = default)
        => TransformTextAsync(text, systemPrompt, cancellationToken);

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

    /// <summary>
    /// Default backwards-compatible overload. Forwards to the
    /// options-aware overload with <c>null</c> options.
    /// </summary>
    public virtual Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
        => TransformTextAsync(text, systemPrompt, options: null, cancellationToken);

    /// <summary>
    /// Options-aware transform. Concrete providers override this to honour
    /// reasoning effort and any future per-call overrides.
    /// </summary>
    public abstract Task<string> TransformTextAsync(
        string text,
        string systemPrompt,
        TransformOptions? options,
        CancellationToken cancellationToken = default);

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
