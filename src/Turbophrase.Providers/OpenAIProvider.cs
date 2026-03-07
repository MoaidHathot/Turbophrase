using OpenAI;
using OpenAI.Chat;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for OpenAI API.
/// </summary>
public class OpenAIProvider : AIProviderBase
{
    private const string DefaultModel = "gpt-4o";
    private const int DefaultMaxTokens = 4096;
    private const float DefaultTemperature = 0.7f;

    private readonly ChatClient _client;

    public OpenAIProvider(string name, ProviderConfig config) : base(name, config)
    {
        var apiKey = config.ApiKey ?? throw new InvalidOperationException("OpenAI API key is required");
        var model = GetModelOrDefault(DefaultModel);
        _client = new ChatClient(model, apiKey);
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
        return !string.IsNullOrEmpty(Config.ApiKey);
    }
}
