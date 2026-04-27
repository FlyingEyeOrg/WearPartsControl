using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;

namespace WearPartsControl.ApplicationServices;

public sealed class UiBusyService : ObservableObject, IUiBusyService
{
    private readonly object _syncRoot = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly List<BusyEntry> _busyEntries = new();
    private int _nextEntryId;
    private bool _isBusy;
    private string _busyMessage = string.Empty;

    public UiBusyService(
        TimeSpan? minimumBusyDuration = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        SynchronizationContext? synchronizationContext = null)
    {
        MinimumBusyDuration = minimumBusyDuration ?? TimeSpan.FromMilliseconds(500);
        _delayAsync = delayAsync ?? Task.Delay;
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
    }

    public TimeSpan MinimumBusyDuration { get; }

    public bool IsBusy
    {
        get
        {
            lock (_syncRoot)
            {
                return _isBusy;
            }
        }
        private set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get
        {
            lock (_syncRoot)
            {
                return _busyMessage;
            }
        }
        private set => SetProperty(ref _busyMessage, value);
    }

    public IDisposable Enter(string? message = null)
    {
        var entry = new BusyEntry(Interlocked.Increment(ref _nextEntryId), message?.Trim());

        lock (_syncRoot)
        {
            _busyEntries.Add(entry);
            ApplyBusyState(_busyEntries.Count > 0, ResolveBusyMessage());
        }

        return new BusyScope(this, entry.Id, DateTimeOffset.UtcNow);
    }

    private async Task ExitAsync(int entryId, DateTimeOffset enteredAt)
    {
        var elapsed = DateTimeOffset.UtcNow - enteredAt;
        var remaining = MinimumBusyDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await _delayAsync(remaining, CancellationToken.None).ConfigureAwait(false);
        }

        lock (_syncRoot)
        {
            _busyEntries.RemoveAll(entry => entry.Id == entryId);
            ApplyBusyState(_busyEntries.Count > 0, ResolveBusyMessage());
        }
    }

    private string ResolveBusyMessage()
    {
        for (var index = _busyEntries.Count - 1; index >= 0; index--)
        {
            var message = _busyEntries[index].Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }

        return string.Empty;
    }

    private void ApplyBusyState(bool isBusy, string busyMessage)
    {
        var isBusyChanged = _isBusy != isBusy;
        var busyMessageChanged = !string.Equals(_busyMessage, busyMessage, StringComparison.Ordinal);
        if (!isBusyChanged && !busyMessageChanged)
        {
            return;
        }

        _isBusy = isBusy;
        _busyMessage = busyMessage;

        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            RaiseBusyStateChanged(isBusyChanged, busyMessageChanged);
            return;
        }

        _synchronizationContext.Post(static state =>
        {
            var payload = ((UiBusyService Owner, bool IsBusyChanged, bool BusyMessageChanged))state!;
            payload.Owner.RaiseBusyStateChanged(payload.IsBusyChanged, payload.BusyMessageChanged);
        }, (this, isBusyChanged, busyMessageChanged));
    }

    private void RaiseBusyStateChanged(bool isBusyChanged, bool busyMessageChanged)
    {
        if (isBusyChanged)
        {
            OnPropertyChanged(nameof(IsBusy));
        }

        if (busyMessageChanged)
        {
            OnPropertyChanged(nameof(BusyMessage));
        }
    }

    private sealed record BusyEntry(int Id, string? Message);

    private sealed class BusyScope : IDisposable
    {
        private UiBusyService? _owner;
        private readonly int _entryId;
        private readonly DateTimeOffset _enteredAt;

        public BusyScope(UiBusyService owner, int entryId, DateTimeOffset enteredAt)
        {
            _owner = owner;
            _entryId = entryId;
            _enteredAt = enteredAt;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
            {
                return;
            }

            _ = owner.ExitAsync(_entryId, _enteredAt);
        }
    }
}
