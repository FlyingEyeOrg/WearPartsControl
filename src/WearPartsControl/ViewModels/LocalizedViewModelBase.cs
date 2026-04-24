using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels;

public abstract class LocalizedViewModelBase : ObservableObject
{
    protected LocalizedViewModelBase()
    {
        WeakEventManager<LocalizationBindingSource, EventArgs>.AddHandler(
            LocalizationBindingSource.Instance,
            nameof(LocalizationBindingSource.Refreshed),
            HandleLocalizationRefreshed);
    }

    protected virtual void OnLocalizationRefreshed()
    {
    }

    private void HandleLocalizationRefreshed(object? sender, EventArgs e)
    {
        OnLocalizationRefreshed();
    }
}