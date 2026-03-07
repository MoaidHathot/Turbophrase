using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// Factory for creating AI providers based on configuration.
/// </summary>
public static class ProviderFactory
{
    /// <summary>
    /// Creates an AI provider instance based on the configuration.
    /// </summary>
    /// <param name="name">The provider name.</param>
    /// <param name="config">The provider configuration.</param>
    /// <returns>The AI provider instance.</returns>
    public static IAIProvider CreateProvider(string name, ProviderConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "openai" => new OpenAIProvider(name, config),
            "azure-openai" => new AzureOpenAIProvider(name, config),
            "anthropic" => new AnthropicProvider(name, config),
            "copilot" or "copilot-cli" or "github-copilot" => new CopilotProvider(name, config),
            "ollama" => new OllamaProvider(name, config),
            _ => throw new ArgumentException($"Unknown provider type: {config.Type}", nameof(config))
        };
    }

    /// <summary>
    /// Creates all providers from the configuration.
    /// </summary>
    /// <param name="turbophraseConfig">The full configuration.</param>
    /// <returns>Dictionary of provider name to provider instance.</returns>
    public static Dictionary<string, IAIProvider> CreateProviders(TurbophraseConfig turbophraseConfig)
    {
        var providers = new Dictionary<string, IAIProvider>();

        foreach (var (name, config) in turbophraseConfig.Providers)
        {
            try
            {
                var provider = CreateProvider(name, config);
                providers[name] = provider;
            }
            catch (Exception)
            {
                // Skip providers that fail to initialize (e.g., missing API keys)
                // They can still be configured but won't be available at runtime
            }
        }

        return providers;
    }

    /// <summary>
    /// Gets the provider to use for a given preset.
    /// </summary>
    /// <param name="preset">The preset.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="providers">Available providers.</param>
    /// <returns>The provider to use.</returns>
    public static IAIProvider GetProviderForPreset(
        PromptPreset preset,
        TurbophraseConfig config,
        Dictionary<string, IAIProvider> providers)
    {
        var providerName = preset.Provider ?? config.DefaultProvider;

        if (providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Provider '{providerName}' is not configured or failed to initialize.");
    }
}
