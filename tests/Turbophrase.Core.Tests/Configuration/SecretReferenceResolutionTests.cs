using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class SecretReferenceResolutionTests : IDisposable
{
    private readonly ISecretsResolver _previousResolver;

    public SecretReferenceResolutionTests()
    {
        _previousResolver = ConfigurationService.SecretsResolver;
    }

    public void Dispose()
    {
        ConfigurationService.SetSecretsResolver(_previousResolver);
    }

    [Fact]
    public void ResolveSecretReference_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(ConfigurationService.ResolveSecretReference(null));
        Assert.Equal(string.Empty, ConfigurationService.ResolveSecretReference(string.Empty));
    }

    [Fact]
    public void ResolveSecretReference_LiteralValue_ReturnsUnchanged()
    {
        var resolved = ConfigurationService.ResolveSecretReference("sk-abc123");
        Assert.Equal("sk-abc123", resolved);
    }

    [Fact]
    public void ResolveSecretReference_EnvironmentVariable_Expanded()
    {
        var marker = $"TURBOPHRASE_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(marker, "secret-value");
        try
        {
            var resolved = ConfigurationService.ResolveSecretReference($"${{{marker}}}");
            Assert.Equal("secret-value", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(marker, null);
        }
    }

    [Fact]
    public void ResolveSecretReference_MissingEnvVar_LeavesPlaceholder()
    {
        var marker = $"TURBOPHRASE_TEST_MISSING_{Guid.NewGuid():N}";
        var resolved = ConfigurationService.ResolveSecretReference($"${{{marker}}}");
        Assert.Equal($"${{{marker}}}", resolved);
    }

    [Fact]
    public void ResolveSecretReference_CredManReference_Resolved()
    {
        ConfigurationService.SetSecretsResolver(new StubResolver(("openai", "stored-key")));
        var resolved = ConfigurationService.ResolveSecretReference("@credman:openai");
        Assert.Equal("stored-key", resolved);
    }

    [Fact]
    public void ResolveSecretReference_UnknownCredManName_KeepsReference()
    {
        ConfigurationService.SetSecretsResolver(new StubResolver());
        var resolved = ConfigurationService.ResolveSecretReference("@credman:missing");
        Assert.Equal("@credman:missing", resolved);
    }

    [Fact]
    public void ResolveSecretReference_NullResolver_FallsBackToReference()
    {
        ConfigurationService.SetSecretsResolver(NullSecretsResolver.Instance);
        var resolved = ConfigurationService.ResolveSecretReference("@credman:anything");
        Assert.Equal("@credman:anything", resolved);
    }

    [Fact]
    public void SetSecretsResolver_NullArgument_FallsBackToNullResolver()
    {
        ConfigurationService.SetSecretsResolver(null!);
        Assert.Same(NullSecretsResolver.Instance, ConfigurationService.SecretsResolver);
    }

    private sealed class StubResolver : ISecretsResolver
    {
        private readonly Dictionary<string, string> _values;

        public StubResolver(params (string name, string value)[] entries)
        {
            _values = entries.ToDictionary(e => e.name, e => e.value, StringComparer.Ordinal);
        }

        public string? TryRead(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : null;
        }
    }
}
