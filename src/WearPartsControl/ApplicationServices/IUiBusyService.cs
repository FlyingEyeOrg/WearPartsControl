using System.ComponentModel;

namespace WearPartsControl.ApplicationServices;

public interface IUiBusyService : INotifyPropertyChanged
{
    TimeSpan MinimumBusyDuration { get; }

    bool IsBusy { get; }

    IDisposable Enter();
}
