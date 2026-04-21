using System.Windows.Threading;

namespace WearPartsControl.ApplicationServices;

public interface IUiDispatcher
{
    void Run(Action action);

    Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal);

    Task RenderAsync();
}