using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Abstractions;

/// <summary>
/// Per-call options forwarded to <see cref="IAIProvider.TransformTextAsync"/>.
/// Currently carries only <see cref="ReasoningEffort"/>, but exists so that
/// future per-call overrides (e.g. temperature) can be added without further
/// breaking changes to the interface.
/// </summary>
/// <param name="ReasoningEffort">
/// Optional reasoning effort override resolved from the active preset. When
/// <c>null</c>, the provider sends no reasoning-related field and the
/// underlying API uses its own default.
/// </param>
public sealed record TransformOptions(ReasoningEffort? ReasoningEffort = null)
{
    /// <summary>
    /// Convenience instance representing "no overrides".
    /// </summary>
    public static TransformOptions None { get; } = new();
}
