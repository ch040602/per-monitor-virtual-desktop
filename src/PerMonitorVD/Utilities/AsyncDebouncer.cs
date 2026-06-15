namespace PerMonitorVD.Utilities;

public sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Func<Task> _action;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public AsyncDebouncer(TimeSpan delay, Func<Task> action)
    {
        _delay = delay;
        _action = action;
    }

    public void Schedule()
    {
        if (_disposed)
            return;

        CancellationTokenSource cts;
        CancellationTokenSource? previous;
        lock (_gate)
        {
            if (_disposed)
                return;

            previous = _cts;
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        previous?.Cancel();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_delay, cts.Token);
                if (_disposed)
                    return;

                await _action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Debounced action failed.");
            }
        });
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
