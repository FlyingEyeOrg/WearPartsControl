using System.ComponentModel;

namespace WearPartsControl.ApplicationServices;

public interface IUiBusyService : INotifyPropertyChanged
{
    bool IsBusy { get; }

    IDisposable Enter();
}
