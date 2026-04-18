using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcConnectionStatusService : ObservableObject, IPlcConnectionStatusService
{
    private readonly SynchronizationContext? _synchronizationContext;
    private PlcStartupConnectionResult _current = PlcStartupConnectionResult.Uninitialized();

    public PlcConnectionStatusService(SynchronizationContext? synchronizationContext = null)
    {
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
    }

    public PlcStartupConnectionResult Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public void Set(PlcStartupConnectionResult result)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            Current = result;
            return;
        }

        _synchronizationContext.Post(static state =>
        {
            var payload = ((PlcConnectionStatusService Owner, PlcStartupConnectionResult Result))state!;
            payload.Owner.Current = payload.Result;
        }, (this, result));
    }
}