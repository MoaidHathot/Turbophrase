using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider for local Ollama instance.
/// </summary>
public class OllamaProvider : AIProviderBase
{
    private const string DefaultEndpoint = "http://localhost:11434";
    private const string DefaultModel = "llama3.2";

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;

    public OllamaProvider(string name, ProviderConfig config) : base(name, config)
    {
        _endpoint = config.Endpoint ?? DefaultEndpoint;
        _model = GetModelOrDefault(DefaultModel);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_endpoint),
            Timeout = TimeSpan.FromMinutes(5) // Ollama can be slow
        };
    }

    public override async Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var request = new OllamaRequest
        {
            Model = _model,
            Prompt = $"{systemPrompt}\n\n{text}",
            Stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);
        return result?.Response?.Trim() ?? string.Empty;
    }

    public override bool ValidateConfiguration()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = _httpClient.GetAsync("/api/tags", cts.Token).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class OllamaRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
