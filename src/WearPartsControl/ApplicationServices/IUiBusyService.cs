using System.ComponentModel;

namespace WearPartsControl.ApplicationServices;

public interface IUiBusyService : INotifyPropertyChanged
{
    TimeSpan MinimumBusyDuration { get; }

    bool IsBusy { get; }

    string BusyMessage { get; }

    IDisposable Enter(string? message = null);
}
