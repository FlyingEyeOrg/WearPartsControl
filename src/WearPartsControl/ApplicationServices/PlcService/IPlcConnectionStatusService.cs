using System.ComponentModel;

namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcConnectionStatusService : INotifyPropertyChanged
{
    PlcStartupConnectionResult Current { get; }

    void Set(PlcStartupConnectionResult result);
}