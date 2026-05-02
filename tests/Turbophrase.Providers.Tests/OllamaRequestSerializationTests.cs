using System.Text.Json;
using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

/// <summary>
/// Tests for the wire-level serialization of the Ollama request body,
/// focusing on the new <c>think</c> field added for reasoning support.
/// </summary>
public class OllamaRequestSerializationTests
{
    private static string Serialize(OllamaProvider.OllamaRequest req)
        => JsonSerializer.Serialize(req);

    [Fact]
    public void Request_WithThinkNull_OmitsThinkField()
    {
        var req = new OllamaProvider.OllamaRequest
        {
            Model = "llama3.2",
            Prompt = "Hello",
            Stream = false,
            Think = null,
        };

        var json = Serialize(req);
        Assert.DoesNotContain("\"think\"", json);
    }

    [Fact]
    public void Request_WithThinkTrue_EmitsThinkTrue()
    {
        var req = new OllamaProvider.OllamaRequest
        {
            Model = "qwen3",
            Prompt = "Hello",
            Stream = false,
            Think = true,
        };

        var json = Serialize(req);
        Assert.Contains("\"think\":true", json);
    }

    [Fact]
    public void Request_WithThinkFalse_EmitsThinkFalse()
    {
        var req = new OllamaProvider.OllamaRequest
        {
            Model = "qwen3",
            Prompt = "Hello",
            Stream = false,
            Think = false,
        };

        var json = Serialize(req);
        Assert.Contains("\"think\":false", json);
    }

    [Fact]
    public void Request_AlwaysIncludesCoreFields()
    {
        var req = new OllamaProvider.OllamaRequest
        {
            Model = "mistral",
            Prompt = "Test prompt",
            Stream = false,
        };

        var json = Serialize(req);
        Assert.Contains("\"model\":\"mistral\"", json);
        Assert.Contains("\"prompt\":\"Test prompt\"", json);
        Assert.Contains("\"stream\":false", json);
    }
}
