using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class ProviderConfigTests
{
    [Fact]
    public void ProviderConfig_DefaultValues_AreCorrect()
    {
        var config = new ProviderConfig();

        Assert.Equal(string.Empty, config.Type);
        Assert.Null(config.ApiKey);
        Assert.Null(config.Endpoint);
        Assert.Null(config.Model);
        Assert.Null(config.DeploymentName);
        Assert.Null(config.MaxTokens);
        Assert.Null(config.Temperature);
    }

    [Fact]
    public void ProviderConfig_OpenAI_CanBeConfigured()
    {
        var config = new ProviderConfig
        {
            Type = "openai",
            ApiKey = "sk-test-key",
            Model = "gpt-4o",
            MaxTokens = 4096,
            Temperature = 0.7
        };

        Assert.Equal("openai", config.Type);
        Assert.Equal("sk-test-key", config.ApiKey);
        Assert.Equal("gpt-4o", config.Model);
        Assert.Equal(4096, config.MaxTokens);
        Assert.Equal(0.7, config.Temperature);
    }

    [Fact]
    public void ProviderConfig_AzureOpenAI_CanBeConfigured()
    {
        var config = new ProviderConfig
        {
            Type = "azure-openai",
            ApiKey = "azure-key",
            Endpoint = "https://my-resource.openai.azure.com",
            DeploymentName = "gpt-4-deployment",
            Model = "gpt-4"
        };

        Assert.Equal("azure-openai", config.Type);
        Assert.Equal("azure-key", config.ApiKey);
        Assert.Equal("https://my-resource.openai.azure.com", config.Endpoint);
        Assert.Equal("gpt-4-deployment", config.DeploymentName);
        Assert.Equal("gpt-4", config.Model);
    }

    [Fact]
    public void ProviderConfig_Ollama_CanBeConfigured()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.2"
        };

        Assert.Equal("ollama", config.Type);
        Assert.Equal("http://localhost:11434", config.Endpoint);
        Assert.Equal("llama3.2", config.Model);
        Assert.Null(config.ApiKey);
    }

    [Fact]
    public void ProviderConfig_Temperature_AcceptsValidRange()
    {
        var configMin = new ProviderConfig { Temperature = 0.0 };
        var configMax = new ProviderConfig { Temperature = 2.0 };
        var configMid = new ProviderConfig { Temperature = 1.0 };

        Assert.Equal(0.0, configMin.Temperature);
        Assert.Equal(2.0, configMax.Temperature);
        Assert.Equal(1.0, configMid.Temperature);
    }
}
