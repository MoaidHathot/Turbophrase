namespace Turbophrase.Core.Configuration;

/// <summary>
/// Resolves opaque secret references such as <c>@credman:openai</c> to their
/// concrete values. Implementations are platform-specific (e.g., the Win32
/// Credential Manager wrapper lives in the Turbophrase tray project).
/// </summary>
public interface ISecretsResolver
{
    /// <summary>
    /// Looks up the secret stored under <paramref name="name"/>. Returns the
    /// value when found, or <c>null</c> if the resolver does not have a value
    /// for that name. Implementations must not throw for missing entries.
    /// </summary>
    string? TryRead(string name);
}

/// <summary>
/// No-op secrets resolver used when no platform-specific implementation has
/// been registered. Returns <c>null</c> for every lookup, which causes
/// configuration to fall back to literal values (for example,
/// <c>@credman:openai</c> remains as-is, surfacing a clear "not configured"
/// state to the provider rather than a crash).
/// </summary>
public sealed class NullSecretsResolver : ISecretsResolver
{
    public static readonly NullSecretsResolver Instance = new();

    public string? TryRead(string name) => null;
}
