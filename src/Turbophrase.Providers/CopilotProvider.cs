using GitHub.Copilot.SDK;
using Turbophrase.Core.Abstractions;

namespace Turbophrase.Providers;

/// <summary>
/// AI provider that uses GitHub Copilot SDK.
/// Connects to the Copilot CLI which handles authentication automatically.
/// </summary>
public class CopilotProvider(string name, Turbophrase.Core.Configuration.ProviderConfig config)
	: AIProviderBase(name, config)
{
    private const string DefaultModel = "gpt-5-mini";

    private CopilotClient? _client;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public override async Task<string> TransformTextAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var client = await GetOrCreateClientAsync();
        var model = GetModelOrDefault(DefaultModel);

        // Create a session with the system message
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            // Disable tools - we only want text transformation
            AvailableTools = new List<string>(),
            // Allow all permissions (we're just doing text transformation)
            OnPermissionRequest = PermissionHandler.ApproveAll
        }, cancellationToken);

        var responseBuilder = new System.Text.StringBuilder();
        var done = new TaskCompletionSource<bool>();
        string? errorMessage = null;

        // Subscribe to session events
        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    responseBuilder.Clear();
                    responseBuilder.Append(msg.Data.Content);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult(true);
                    break;
                case SessionErrorEvent err:
                    errorMessage = err.Data.Message;
                    done.TrySetResult(false);
                    break;
            }
        });

        // Send the message
        await session.SendAsync(new MessageOptions { Prompt = text }, cancellationToken);

        // Wait for completion with cancellation support
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2)); // 2 minute timeout

        try
        {
            var completedTask = await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completedTask != done.Task)
            {
                throw new OperationCanceledException("Request timed out or was cancelled.");
            }
        }
        catch (OperationCanceledException)
        {
            await session.AbortAsync(cancellationToken);
            throw;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            throw new InvalidOperationException($"Copilot error: {errorMessage}");
        }

        var result = responseBuilder.ToString().Trim();
        if (string.IsNullOrEmpty(result))
        {
            throw new InvalidOperationException("Copilot returned an empty response.");
        }

        return result;
    }

    public override bool ValidateConfiguration()
    {
        // The SDK ships with a bundled Copilot CLI, so there is nothing to discover
        // or validate at configuration time. Auth/runtime errors surface during
        // session creation in TransformTextAsync.
        return true;
    }

    private async Task<CopilotClient> GetOrCreateClientAsync()
    {
        if (_client != null && _isInitialized)
        {
            return _client;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_client != null && _isInitialized)
            {
                return _client;
            }

            // Do not set CliPath - the SDK uses its bundled CLI by default.
            // Overriding CliPath previously caused failures on machines where
            // a global copilot install was missing or where npm produced a .cmd
            // shim that .NET's Process.Start could not launch directly.
            _client = new CopilotClient(new CopilotClientOptions
            {
                UseLoggedInUser = true,
                AutoStart = true,
                AutoRestart = true
            });

            await _client.StartAsync();
            _isInitialized = true;

            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Disposes the Copilot client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            _client = null;
            _isInitialized = false;
        }
    }
}
