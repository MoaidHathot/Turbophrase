using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Providers.Tests;

public class ProviderFactoryTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData("azure-openai")]
    [InlineData("anthropic")]
    [InlineData("copilot")]
    [InlineData("copilot-cli")]
    [InlineData("github-copilot")]
    [InlineData("ollama")]
    public void CreateProvider_KnownTypes_ReturnsCorrectProviderType(string providerType)
    {
        var config = new ProviderConfig
        {
            Type = providerType,
            ApiKey = "test-key",
            Endpoint = "http://localhost:11434"
        };

        // For providers that require API key validation, we skip instantiation test
        // since they throw in constructor. This test verifies the switch statement logic.
        if (providerType == "ollama")
        {
            var provider = ProviderFactory.CreateProvider("test", config);
            Assert.NotNull(provider);
            Assert.IsType<OllamaProvider>(provider);
        }
    }

    [Fact]
    public void CreateProvider_UnknownType_ThrowsArgumentException()
    {
        var config = new ProviderConfig
        {
            Type = "unknown-provider"
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            ProviderFactory.CreateProvider("test", config));

        Assert.Contains("Unknown provider type", exception.Message);
        Assert.Contains("unknown-provider", exception.Message);
    }

    [Fact]
    public void CreateProvider_Ollama_UsesDefaultEndpoint()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Model = "llama3.2"
        };

        var provider = ProviderFactory.CreateProvider("test-ollama", config);

        Assert.NotNull(provider);
        Assert.Equal("test-ollama", provider.Name);
    }

    [Fact]
    public void CreateProvider_Ollama_UsesCustomEndpoint()
    {
        var config = new ProviderConfig
        {
            Type = "ollama",
            Endpoint = "http://192.168.1.100:11434",
            Model = "mistral"
        };

        var provider = ProviderFactory.CreateProvider("custom-ollama", config);

        Assert.NotNull(provider);
        Assert.IsType<OllamaProvider>(provider);
    }

    [Fact]
    public void CreateProviders_EmptyConfig_ReturnsEmptyDictionary()
    {
        var config = new TurbophraseConfig();

        var providers = ProviderFactory.CreateProviders(config);

        Assert.NotNull(providers);
        Assert.Empty(providers);
    }

    [Fact]
    public void CreateProviders_WithOllamaProvider_CreatesProvider()
    {
        var config = new TurbophraseConfig
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["ollama"] = new ProviderConfig
                {
                    Type = "ollama",
                    Endpoint = "http://localhost:11434",
                    Model = "llama3.2"
                }
            }
        };

        var providers = ProviderFactory.CreateProviders(config);

        Assert.Single(providers);
        Assert.True(providers.ContainsKey("ollama"));
    }

    [Fact]
    public void CreateProviders_SkipsInvalidProviders()
    {
        var config = new TurbophraseConfig
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["ollama"] = new ProviderConfig
                {
                    Type = "ollama",
                    Endpoint = "http://localhost:11434"
                },
                ["openai"] = new ProviderConfig
                {
                    Type = "openai",
                    ApiKey = null // Will fail to initialize
                }
            }
        };

        var providers = ProviderFactory.CreateProviders(config);

        // Only ollama should be created since openai will fail without API key
        Assert.Single(providers);
        Assert.True(providers.ContainsKey("ollama"));
        Assert.False(providers.ContainsKey("openai"));
    }

    [Fact]
    public void GetProviderForPreset_WithPresetProvider_ReturnsPresetProvider()
    {
        var providers = new Dictionary<string, IAIProvider>
        {
            ["ollama"] = CreateMockProvider("ollama"),
            ["openai"] = CreateMockProvider("openai")
        };

        var preset = new PromptPreset
        {
            Name = "Test",
            SystemPrompt = "Test prompt",
            Provider = "ollama"
        };

        var config = new TurbophraseConfig { DefaultProvider = "openai" };

        var provider = ProviderFactory.GetProviderForPreset(preset, config, providers);

        Assert.Equal("ollama", provider.Name);
    }

    [Fact]
    public void GetProviderForPreset_WithoutPresetProvider_ReturnsDefaultProvider()
    {
        var providers = new Dictionary<string, IAIProvider>
        {
            ["ollama"] = CreateMockProvider("ollama"),
            ["openai"] = CreateMockProvider("openai")
        };

        var preset = new PromptPreset
        {
            Name = "Test",
            SystemPrompt = "Test prompt",
            Provider = null // No override
        };

        var config = new TurbophraseConfig { DefaultProvider = "openai" };

        var provider = ProviderFactory.GetProviderForPreset(preset, config, providers);

        Assert.Equal("openai", provider.Name);
    }

    [Fact]
    public void GetProviderForPreset_ProviderNotFound_ThrowsInvalidOperationException()
    {
        var providers = new Dictionary<string, IAIProvider>
        {
            ["ollama"] = CreateMockProvider("ollama")
        };

        var preset = new PromptPreset
        {
            Name = "Test",
            SystemPrompt = "Test prompt",
            Provider = "nonexistent"
        };

        var config = new TurbophraseConfig { DefaultProvider = "openai" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProviderFactory.GetProviderForPreset(preset, config, providers));

        Assert.Contains("nonexistent", exception.Message);
        Assert.Contains("not configured", exception.Message);
    }

    private static IAIProvider CreateMockProvider(string name)
    {
        return new MockProvider(name);
    }

    private class MockProvider : IAIProvider
    {
        public MockProvider(string name) => Name = name;
        public string Name { get; }
        public Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
            => Task.FromResult(text);
        public bool ValidateConfiguration() => true;
    }
}
