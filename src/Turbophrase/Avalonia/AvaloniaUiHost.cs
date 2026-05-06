using Avalonia;
using Avalonia.Threading;
using AWindow = Avalonia.Controls.Window;

namespace Turbophrase.Avalonia;

public static class AvaloniaUiHost
{
    private static int _initialized;
    private static readonly ManualResetEventSlim Ready = new();
    private static Exception? _startupException;

    public static void EnsureInitialized()
    {
        if (Volatile.Read(ref _initialized) == 1)
        {
            Ready.Wait();
            if (_startupException != null)
            {
                throw new InvalidOperationException("Avalonia UI failed to start.", _startupException);
            }
            return;
        }

        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            var thread = new Thread(RunUiThread)
            {
                Name = "Turbophrase Avalonia UI",
                IsBackground = true,
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        Ready.Wait();
        if (_startupException != null)
        {
            throw new InvalidOperationException("Avalonia UI failed to start.", _startupException);
        }
    }

    private static void RunUiThread()
    {
        try
        {
            TurbophraseAvaloniaApp.BuildAvaloniaApp().SetupWithoutStarting();
            Ready.Set();
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _startupException = ex;
            Ready.Set();
        }
    }

    public static T Invoke<T>(Func<T> action)
    {
        EnsureInitialized();

        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return Dispatcher.UIThread.Invoke(action);
    }

    public static void Invoke(Action action)
    {
        EnsureInitialized();

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Invoke(action);
    }

    public static Task<T> InvokeAsync<T>(Func<T> action)
    {
        EnsureInitialized();

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(action());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    public static Task ShowWindowAsync(AWindow window)
    {
        EnsureInitialized();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Show()
        {
            window.Closed += OnClosed;
            try
            {
                window.Show();
            }
            catch (Exception ex)
            {
                window.Closed -= OnClosed;
                completion.TrySetException(ex);
            }
        }

        void OnClosed(object? sender, EventArgs e)
        {
            window.Closed -= OnClosed;
            completion.TrySetResult();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Show();
        }
        else
        {
            Dispatcher.UIThread.Invoke(Show);
        }

        return completion.Task;
    }

    public static Task ShowStandaloneWindowAsync(Func<AWindow> createWindow)
    {
        EnsureInitialized();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            AWindow? window = null;
            try
            {
                window = createWindow();
                window.Closed += OnClosed;
                window.Show();
                window.Activate();
            }
            catch (Exception ex)
            {
                if (window != null)
                {
                    window.Closed -= OnClosed;
                }
                completion.TrySetException(ex);
            }

            void OnClosed(object? sender, EventArgs e)
            {
                if (window != null)
                {
                    window.Closed -= OnClosed;
                }
                completion.TrySetResult();
            }
        });

        return completion.Task;
    }
}
