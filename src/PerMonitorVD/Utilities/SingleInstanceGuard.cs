namespace PerMonitorVD.Utilities;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsOwner = createdNew;
    }

    public bool IsOwner { get; }

    public void Dispose()
    {
        if (IsOwner)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }

        _mutex.Dispose();
    }
}
