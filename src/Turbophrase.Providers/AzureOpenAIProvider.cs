using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for Azure OpenAI Service. Stays on the Chat Completions
/// surface (Azure.AI.OpenAI 2.1.0 stable doesn't expose ResponsesClient),
/// but the bumped transitive OpenAI 2.10 dependency means
/// <see cref="ChatCompletionOptions.ReasoningEffortLevel"/> is now a
/// typed property — no raw-JSON injection needed.
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
        var deploymentName = config.DeploymentName ?? config.Model ?? throw new InvalidOperationException("Azure OpenAI deployment name is required");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _client = azureClient.GetChatClient(deploymentName);
    }

    public override async Task<string> TransformTextAsync(
        string text,
        string systemPrompt,
        TransformOptions? options,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(text)
        };

        var requestOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = GetMaxTokensOrDefault(DefaultMaxTokens),
        };

        var reasoning = ReasoningEffortMapping.ToOpenAIChat(options?.ReasoningEffort);
        if (reasoning is not null)
        {
            requestOptions.ReasoningEffortLevel = reasoning;
            // Reasoning-capable models reject temperature; omit it.
        }
        else
        {
            requestOptions.Temperature = GetTemperatureOrDefault(DefaultTemperature);
        }

        var response = await _client.CompleteChatAsync(messages, requestOptions, cancellationToken);
        return response.Value.Content[0].Text ?? string.Empty;
    }

    public override bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(Config.Endpoint) &&
               !string.IsNullOrEmpty(Config.ApiKey) &&
               (!string.IsNullOrEmpty(Config.DeploymentName) || !string.IsNullOrEmpty(Config.Model));
    }
}
