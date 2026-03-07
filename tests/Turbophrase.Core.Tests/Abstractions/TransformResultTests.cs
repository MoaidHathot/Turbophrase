using Turbophrase.Core.Abstractions;

namespace Turbophrase.Core.Tests.Abstractions;

public class TransformResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = TransformResult.Ok("transformed text", "openai");

        Assert.True(result.Success);
        Assert.Equal("transformed text", result.TransformedText);
        Assert.Equal("openai", result.ProviderName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Ok_WithEmptyText_IsStillSuccessful()
    {
        var result = TransformResult.Ok("", "openai");

        Assert.True(result.Success);
        Assert.Equal("", result.TransformedText);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var result = TransformResult.Fail("API error occurred", "openai");

        Assert.False(result.Success);
        Assert.Equal("API error occurred", result.ErrorMessage);
        Assert.Equal("openai", result.ProviderName);
        Assert.Null(result.TransformedText);
    }

    [Fact]
    public void Fail_WithoutProvider_HasNullProviderName()
    {
        var result = TransformResult.Fail("Unknown error");

        Assert.False(result.Success);
        Assert.Equal("Unknown error", result.ErrorMessage);
        Assert.Null(result.ProviderName);
    }

    [Fact]
    public void TransformResult_CanBeCreatedDirectly()
    {
        var result = new TransformResult
        {
            Success = true,
            TransformedText = "test",
            ProviderName = "test-provider"
        };

        Assert.True(result.Success);
        Assert.Equal("test", result.TransformedText);
        Assert.Equal("test-provider", result.ProviderName);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("azure-openai")]
    [InlineData("copilot")]
    [InlineData("ollama")]
    public void Ok_WorksWithAllProviderNames(string providerName)
    {
        var result = TransformResult.Ok("test", providerName);

        Assert.True(result.Success);
        Assert.Equal(providerName, result.ProviderName);
    }

    [Theory]
    [InlineData("Rate limit exceeded")]
    [InlineData("Invalid API key")]
    [InlineData("Connection timeout")]
    [InlineData("Model not found")]
    public void Fail_WorksWithVariousErrorMessages(string errorMessage)
    {
        var result = TransformResult.Fail(errorMessage, "openai");

        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }
}
