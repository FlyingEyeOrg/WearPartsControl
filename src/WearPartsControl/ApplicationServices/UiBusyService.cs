using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;

namespace WearPartsControl.ApplicationServices;

public sealed class UiBusyService : ObservableObject, IUiBusyService
{
    private readonly object _syncRoot = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SynchronizationContext? _synchronizationContext;
    private int _busyCount;
    private bool _isBusy;

    public UiBusyService(
        TimeSpan? minimumBusyDuration = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        SynchronizationContext? synchronizationContext = null)
    {
        MinimumBusyDuration = minimumBusyDuration ?? TimeSpan.FromSeconds(1);
        _delayAsync = delayAsync ?? Task.Delay;
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
    }

    public TimeSpan MinimumBusyDuration { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public IDisposable Enter()
    {
        lock (_syncRoot)
        {
            _busyCount++;
            SetIsBusy(_busyCount > 0);
        }

        return new BusyScope(this, DateTimeOffset.UtcNow);
    }

    private async Task ExitAsync(DateTimeOffset enteredAt)
    {
        var elapsed = DateTimeOffset.UtcNow - enteredAt;
        var remaining = MinimumBusyDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await _delayAsync(remaining, CancellationToken.None).ConfigureAwait(false);
        }

        lock (_syncRoot)
        {
            if (_busyCount > 0)
            {
                _busyCount--;
            }

            SetIsBusy(_busyCount > 0);
        }
    }

    private void SetIsBusy(bool value)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            IsBusy = value;
            return;
        }

        _synchronizationContext.Post(static state =>
        {
            var payload = ((UiBusyService Owner, bool Value))state!;
            payload.Owner.IsBusy = payload.Value;
        }, (this, value));
    }

    private sealed class BusyScope : IDisposable
    {
        private UiBusyService? _owner;
        private readonly DateTimeOffset _enteredAt;

        public BusyScope(UiBusyService owner, DateTimeOffset enteredAt)
        {
            _owner = owner;
            _enteredAt = enteredAt;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
            {
                return;
            }

            _ = owner.ExitAsync(_enteredAt);
        }
    }
}
