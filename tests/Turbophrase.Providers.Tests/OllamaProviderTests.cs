using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

public class OllamaProviderTests
{
    [Fact]
    public void Constructor_WithDefaultConfig_CreatesProvider()
    {
        var config = new ProviderConfig
        {
            Type = "ollama"
        };

        var provider = new OllamaProvider("test-ollama", config);

        Assert.NotNull(provider);
        Assert.Equal("test-ollama", provider.Name);
    }

    [Fact]
    public void Constructor_WithCustomEndpoint_CreatesProvider()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Endpoint = "http://192.168.1.100:11434",
            Model = "mistral"
        };

        var provider = new OllamaProvider("custom-ollama", config);

        Assert.NotNull(provider);
        Assert.Equal("custom-ollama", provider.Name);
    }

    [Fact]
    public void Constructor_WithCustomModel_CreatesProvider()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Model = "codellama"
        };

        var provider = new OllamaProvider("codellama-provider", config);

        Assert.NotNull(provider);
    }

    [Fact]
    public void ValidateConfiguration_WithNoRunningServer_ReturnsFalse()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Endpoint = "http://localhost:59999" // Valid port that is unlikely to be running
        };

        var provider = new OllamaProvider("test", config);

        // This should return false since there's no server at this port
        var isValid = provider.ValidateConfiguration();

        Assert.False(isValid);
    }
}
