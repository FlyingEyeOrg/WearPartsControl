using System.Threading;

namespace WearPartsControl.ApplicationServices.Startup;

internal sealed class AppSingleInstanceLease : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    private AppSingleInstanceLease(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static bool TryAcquire(string mutexName, out AppSingleInstanceLease? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        var mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            lease = null;
            return false;
        }

        lease = new AppSingleInstanceLease(mutex);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}