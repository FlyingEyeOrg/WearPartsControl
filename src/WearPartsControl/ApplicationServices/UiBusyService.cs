using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ApplicationServices;

public sealed class UiBusyService : ObservableObject, IUiBusyService
{
    private readonly object _syncRoot = new();
    private int _busyCount;
    private bool _isBusy;

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
            IsBusy = _busyCount > 0;
        }

        return new BusyScope(this);
    }

    private void Exit()
    {
        lock (_syncRoot)
        {
            if (_busyCount > 0)
            {
                _busyCount--;
            }

            IsBusy = _busyCount > 0;
        }
    }

    private sealed class BusyScope : IDisposable
    {
        private UiBusyService? _owner;

        public BusyScope(UiBusyService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Exit();
        }
    }
}
