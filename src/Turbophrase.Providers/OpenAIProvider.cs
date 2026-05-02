using OpenAI.Responses;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for OpenAI API. Uses the Responses API (the OpenAI 2.x
/// SDK's recommended surface for gpt-5 / o-series models, and the only
/// path with first-class reasoning support).
/// </summary>
public class OpenAIProvider : AIProviderBase
{
    private const string DefaultModel = "gpt-4o";
    private const int DefaultMaxTokens = 4096;
    private const float DefaultTemperature = 0.7f;

    private readonly ResponsesClient _client;
    private readonly string _model;

    public OpenAIProvider(string name, ProviderConfig config) : base(name, config)
    {
        var apiKey = config.ApiKey ?? throw new InvalidOperationException("OpenAI API key is required");
        _model = GetModelOrDefault(DefaultModel);
        _client = new ResponsesClient(apiKey);
    }

    public override async Task<string> TransformTextAsync(
        string text,
        string systemPrompt,
        TransformOptions? options,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = new CreateResponseOptions
        {
            Model = _model,
            Instructions = systemPrompt,
            MaxOutputTokenCount = GetMaxTokensOrDefault(DefaultMaxTokens),
        };

        // Reasoning models (gpt-5, o-series) on the Responses API ignore
        // temperature; only set it when not running with reasoning so we
        // don't surprise users with a 400.
        var reasoning = ReasoningEffortMapping.ToOpenAIResponses(options?.ReasoningEffort);
        if (reasoning is not null)
        {
            requestOptions.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = reasoning,
            };
        }
        else
        {
            requestOptions.Temperature = GetTemperatureOrDefault(DefaultTemperature);
        }

        requestOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(text));

        var response = await _client.CreateResponseAsync(requestOptions, cancellationToken);
        return response.Value.GetOutputText() ?? string.Empty;
    }

    public override bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(Config.ApiKey);
    }
}
