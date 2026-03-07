using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for Anthropic Claude API.
/// </summary>
public class AnthropicProvider : AIProviderBase
{
    private const string DefaultModel = "claude-sonnet-4-20250514";
    private const int DefaultMaxTokens = 4096;

    private readonly AnthropicClient _client;

    public AnthropicProvider(string name, ProviderConfig config) : base(name, config)
    {
        var apiKey = config.ApiKey ?? throw new InvalidOperationException("Anthropic API key is required");
        _client = new AnthropicClient(apiKey);
    }

    public override async Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var messages = new List<Message>
        {
            new Message(RoleType.User, text)
        };

        var parameters = new MessageParameters
        {
            Model = GetModelOrDefault(DefaultModel),
            MaxTokens = GetMaxTokensOrDefault(DefaultMaxTokens),
            System = [new SystemMessage(systemPrompt)],
            Messages = messages
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
        
        return response.Message.ToString();
    }

    public override bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(Config.ApiKey);
    }
}
