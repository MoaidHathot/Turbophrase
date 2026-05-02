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

    public override async Task<string> TransformTextAsync(
        string text,
        string systemPrompt,
        TransformOptions? options,
        CancellationToken cancellationToken = default)
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

        var anthropicEffort = ReasoningEffortMapping.ToAnthropic(options?.ReasoningEffort);
        if (anthropicEffort is not null)
        {
            // Adaptive thinking: model decides when to think and how
            // hard, guided by the effort hint. With thinking enabled
            // Anthropic requires temperature == 1.0 (or unset).
            parameters.Thinking = new ThinkingParameters
            {
                Type = ThinkingType.adaptive,
            };
            parameters.OutputConfig = new OutputConfig
            {
                Effort = anthropicEffort,
            };
            parameters.Temperature = 1.0m;
        }

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        return response.Message.ToString();
    }

    public override bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(Config.ApiKey);
    }
}
