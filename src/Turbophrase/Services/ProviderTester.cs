using System.Diagnostics;
using Turbophrase.Core.Configuration;
using Turbophrase.Providers;

namespace Turbophrase.Services;

/// <summary>
/// Issues a single small request against a provider configuration to verify
/// connectivity and credentials. Used by the Settings UI's "Test connection"
/// button and by the <c>turbophrase test</c> CLI command.
/// </summary>
public static class ProviderTester
{
    private const string TestSystemPrompt = "Reply with the single word 'pong' and nothing else.";
    private const string TestUserText = "ping";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Result of a connection test.
    /// </summary>
    public sealed class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? Response { get; init; }
        public TimeSpan Elapsed { get; init; }
    }

    /// <summary>
    /// Tests a provider configuration that may not yet be persisted to disk.
    /// Secret references (<c>${ENV}</c>, <c>@credman:</c>) are expected to be
    /// resolved already by the caller -- the Settings UI passes the in-memory
    /// values, and CLI callers go through <see cref="ConfigurationService"/>.
    /// </summary>
    public static async Task<Result> TestAsync(
        string providerName,
        ProviderConfig config,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return new Result { Success = false, ErrorMessage = "Provider name is required." };
        }

        if (config == null)
        {
            return new Result { Success = false, ErrorMessage = "Provider configuration is required." };
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var provider = ProviderFactory.CreateProvider(providerName, config);

            if (!provider.ValidateConfiguration())
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Provider '{providerName}' is not properly configured. Check the API key and required fields.",
                    Elapsed = stopwatch.Elapsed,
                };
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? DefaultTimeout);

            var response = await provider.TransformTextAsync(TestUserText, TestSystemPrompt, cts.Token);
            stopwatch.Stop();

            return new Result
            {
                Success = true,
                Response = response,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new Result { Success = false, ErrorMessage = "Test cancelled.", Elapsed = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            return new Result { Success = false, ErrorMessage = $"Test timed out after {(timeout ?? DefaultTimeout).TotalSeconds:F0}s.", Elapsed = stopwatch.Elapsed };
        }
        catch (ArgumentException ex)
        {
            return new Result { Success = false, ErrorMessage = ex.Message, Elapsed = stopwatch.Elapsed };
        }
        catch (Exception ex)
        {
            return new Result { Success = false, ErrorMessage = ex.Message, Elapsed = stopwatch.Elapsed };
        }
    }
}
