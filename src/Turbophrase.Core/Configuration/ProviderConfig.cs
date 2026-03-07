namespace Turbophrase.Core.Configuration;

/// <summary>
/// Configuration for an AI provider.
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// The type of provider (openai, azure-openai, anthropic, copilot-cli, ollama).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// API key for the provider. Supports environment variable references like ${ENV_VAR}.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Endpoint URL for the provider (used by Azure OpenAI and Ollama).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Model name or deployment name to use.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Deployment name for Azure OpenAI.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Optional maximum tokens for the response.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Optional temperature for response generation (0.0-2.0).
    /// </summary>
    public double? Temperature { get; set; }
}
