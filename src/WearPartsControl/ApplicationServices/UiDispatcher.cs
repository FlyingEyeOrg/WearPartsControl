using System.Windows;
using System.Windows.Threading;

namespace WearPartsControl.ApplicationServices;

public sealed class UiDispatcher : IUiDispatcher
{
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }

    public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, priority).Task;
        }

        action();
        return Task.CompletedTask;
    }

    public async Task RenderAsync()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            return;
        }

        await Task.Yield();
    }
}