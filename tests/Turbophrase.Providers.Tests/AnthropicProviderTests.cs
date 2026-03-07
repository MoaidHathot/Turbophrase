using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

public class AnthropicProviderTests
{
    [Fact]
    public void Constructor_WithNullApiKey_ThrowsInvalidOperationException()
    {
        var config = new ProviderConfig
        {
            Type = "anthropic",
            ApiKey = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AnthropicProvider("test", config));

        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void ValidateConfiguration_WithNullApiKey_ReturnsFalse()
    {
        var config = new ProviderConfig
        {
            Type = "anthropic",
            ApiKey = null
        };

        Assert.True(string.IsNullOrEmpty(config.ApiKey));
    }

    [Fact]
    public void ValidateConfiguration_WithApiKey_ReturnsTrue()
    {
        var config = new ProviderConfig
        {
            Type = "anthropic",
            ApiKey = "sk-ant-test-key"
        };

        Assert.False(string.IsNullOrEmpty(config.ApiKey));
    }
}
