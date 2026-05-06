using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for Azure OpenAI Service.
/// </summary>
public class AzureOpenAIProvider : AIProviderBase
{
    private const int DefaultMaxTokens = 4096;
    private const float DefaultTemperature = 0.7f;

    private readonly ChatClient _client;

    public AzureOpenAIProvider(string name, ProviderConfig config) : base(name, config)
    {
        var endpoint = config.Endpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint is required");
        var apiKey = config.ApiKey ?? throw new InvalidOperationException("Azure OpenAI API key is required");
        var (resourceEndpoint, deploymentFromEndpoint) = NormalizeEndpoint(endpoint);
        var deploymentName = config.DeploymentName ?? config.Model ?? deploymentFromEndpoint ?? throw new InvalidOperationException("Azure OpenAI deployment name is required");

        var azureClient = new AzureOpenAIClient(resourceEndpoint, new AzureKeyCredential(apiKey));
        _client = azureClient.GetChatClient(deploymentName);
    }

    public override async Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(text)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = GetMaxTokensOrDefault(DefaultMaxTokens),
            Temperature = GetTemperatureOrDefault(DefaultTemperature)
        };

        var response = await _client.CompleteChatAsync(messages, options, cancellationToken);
        return response.Value.Content[0].Text ?? string.Empty;
    }

    public override bool ValidateConfiguration()
    {
        return GetConfigurationError() == null;
    }

    public override string? GetConfigurationError()
    {
        if (IsMissing(Config.Endpoint))
        {
            return $"Provider '{Name}' is missing Azure OpenAI endpoint.";
        }

        if (IsMissing(Config.ApiKey))
        {
            return $"Provider '{Name}' is missing Azure OpenAI API key.";
        }

        if (IsMissing(Config.DeploymentName) && IsMissing(Config.Model) && TryGetDeploymentFromEndpoint(Config.Endpoint) == null)
        {
            return $"Provider '{Name}' is missing Azure OpenAI deployment name.";
        }

        return null;
    }

    private static (Uri ResourceEndpoint, string? DeploymentName) NormalizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (trimmed.StartsWith("${", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Azure OpenAI endpoint environment variable was not resolved: {trimmed}");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Azure OpenAI endpoint is not a valid absolute URI: {trimmed}");
        }

        var deploymentName = TryGetDeploymentFromEndpoint(uri);
        if (deploymentName == null)
        {
            return (uri, null);
        }

        return (new Uri($"{uri.Scheme}://{uri.Authority}"), deploymentName);
    }

    private static string? TryGetDeploymentFromEndpoint(string? endpoint)
    {
        return Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
            ? TryGetDeploymentFromEndpoint(uri)
            : null;
    }

    private static string? TryGetDeploymentFromEndpoint(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], "deployments", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(segments[i + 1]);
            }
        }

        return null;
    }

    private static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value)
        || value.StartsWith("${", StringComparison.Ordinal)
        || value.StartsWith(ConfigurationService.CredManPrefix, StringComparison.Ordinal);
}
