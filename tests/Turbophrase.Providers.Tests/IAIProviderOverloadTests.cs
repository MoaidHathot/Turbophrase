using Turbophrase.Core.Abstractions;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Providers.Tests;

/// <summary>
/// Tests for the <see cref="IAIProvider"/> overload semantics: the new
/// options-aware overload should be the canonical path, and the
/// legacy 2-arg overload should forward to it with <c>null</c> options.
/// </summary>
public class IAIProviderOverloadTests
{
    /// <summary>
    /// Concrete fake that overrides only the options-aware overload and
    /// records what it received. Proves that the base class's legacy
    /// overload forwards correctly without needing a concrete provider.
    /// </summary>
    private sealed class RecordingProvider : AIProviderBase
    {
        public TransformOptions? CapturedOptions { get; private set; } = TransformOptions.None;
        public bool WasInvoked { get; private set; }

        public RecordingProvider() : base("recorder", new ProviderConfig { Type = "recorder" }) { }

        public override Task<string> TransformTextAsync(string text, string systemPrompt, TransformOptions? options, CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            CapturedOptions = options;
            return Task.FromResult($"{text}|{options?.ReasoningEffort?.ToString() ?? "<null>"}");
        }

        public override bool ValidateConfiguration() => true;
    }

    [Fact]
    public async Task LegacyOverload_ForwardsToOptionsAwareOverloadWithNull()
    {
        var p = new RecordingProvider();

        var result = await p.TransformTextAsync("hello", "system");

        Assert.True(p.WasInvoked);
        Assert.Null(p.CapturedOptions);
        Assert.Equal("hello|<null>", result);
    }

    [Fact]
    public async Task OptionsAwareOverload_PassesOptionsThrough()
    {
        var p = new RecordingProvider();
        var opts = new TransformOptions(ReasoningEffort.High);

        var result = await p.TransformTextAsync("hello", "system", opts);

        Assert.True(p.WasInvoked);
        Assert.NotNull(p.CapturedOptions);
        Assert.Equal(ReasoningEffort.High, p.CapturedOptions!.ReasoningEffort);
        Assert.Equal("hello|High", result);
    }

    [Fact]
    public void TransformOptions_None_HasNullReasoningEffort()
    {
        Assert.Null(TransformOptions.None.ReasoningEffort);
    }

    [Fact]
    public void TransformOptions_DefaultConstructor_HasNullReasoningEffort()
    {
        Assert.Null(new TransformOptions().ReasoningEffort);
    }

    [Fact]
    public void TransformOptions_RecordEquality_BasedOnFields()
    {
        var a = new TransformOptions(ReasoningEffort.Medium);
        var b = new TransformOptions(ReasoningEffort.Medium);
        var c = new TransformOptions(ReasoningEffort.High);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
