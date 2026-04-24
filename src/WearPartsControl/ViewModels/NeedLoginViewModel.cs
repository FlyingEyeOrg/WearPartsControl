using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels;

public sealed class NeedLoginViewModel : ObservableObject
{
    public NeedLoginViewModel()
    {
        WeakEventManager<LocalizationBindingSource, EventArgs>.AddHandler(
            LocalizationBindingSource.Instance,
            nameof(LocalizationBindingSource.Refreshed),
            OnLocalizationRefreshed);
    }

    public string Title => LocalizedText.Get("ViewModels.NeedLoginVm.Title");

    public string Description => LocalizedText.Get("ViewModels.NeedLoginVm.Description");

    public string Hint => LocalizedText.Get("ViewModels.NeedLoginVm.Hint");

    private void OnLocalizationRefreshed(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Hint));
    }
}