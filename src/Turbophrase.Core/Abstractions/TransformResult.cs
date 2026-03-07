namespace Turbophrase.Core.Abstractions;

/// <summary>
/// Result of a text transformation operation.
/// </summary>
public class TransformResult
{
    /// <summary>
    /// Whether the transformation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The transformed text (if successful).
    /// </summary>
    public string? TransformedText { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The provider that was used.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TransformResult Ok(string transformedText, string providerName)
        => new() { Success = true, TransformedText = transformedText, ProviderName = providerName };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TransformResult Fail(string errorMessage, string? providerName = null)
        => new() { Success = false, ErrorMessage = errorMessage, ProviderName = providerName };
}
