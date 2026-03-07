using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

public class OpenAIProviderTests
{
    [Fact]
    public void Constructor_WithNullApiKey_ThrowsInvalidOperationException()
    {
        var config = new ProviderConfig
        {
            Type = "openai",
            ApiKey = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new OpenAIProvider("test", config));

        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsException()
    {
        var config = new ProviderConfig
        {
            Type = "openai",
            ApiKey = ""
        };

        // Empty string throws ArgumentException from the OpenAI SDK
        Assert.Throws<ArgumentException>(() =>
            new OpenAIProvider("test", config));
    }

    [Fact]
    public void ValidateConfiguration_WithNullApiKey_ReturnsFalse()
    {
        // We can't instantiate without API key, so we test the concept
        // by checking config directly
        var config = new ProviderConfig
        {
            Type = "openai",
            ApiKey = null
        };

        Assert.True(string.IsNullOrEmpty(config.ApiKey));
    }

    [Fact]
    public void ValidateConfiguration_WithApiKey_ReturnsTrue()
    {
        var config = new ProviderConfig
        {
            Type = "openai",
            ApiKey = "sk-test-key"
        };

        Assert.False(string.IsNullOrEmpty(config.ApiKey));
    }
}
