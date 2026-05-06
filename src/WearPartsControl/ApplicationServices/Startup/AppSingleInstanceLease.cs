using System.Threading;

namespace WearPartsControl.ApplicationServices.Startup;

internal sealed class AppSingleInstanceLease : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private RegisteredWaitHandle? _registeredActivationWait;
    private bool _disposed;

    private AppSingleInstanceLease(Mutex mutex, EventWaitHandle activationEvent)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
    }

    public static bool TryAcquire(string instanceName, out AppSingleInstanceLease? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var mutex = new Mutex(initiallyOwned: true, name: BuildMutexName(instanceName), createdNew: out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            lease = null;
            return false;
        }

        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, BuildActivationEventName(instanceName));
        lease = new AppSingleInstanceLease(mutex, activationEvent);
        return true;
    }

    public static void SignalActivation(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        using var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, BuildActivationEventName(instanceName));
        activationEvent.Set();
    }

    public void RegisterActivationCallback(Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);

        _registeredActivationWait ??= ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            static (state, _) => ((Action)state!).Invoke(),
            callback,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registeredActivationWait?.Unregister(null);
        _registeredActivationWait = null;
        _activationEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static string BuildMutexName(string instanceName) => $@"Local\{instanceName}.SingleInstance";

    private static string BuildActivationEventName(string instanceName) => $@"Local\{instanceName}.Activate";
}