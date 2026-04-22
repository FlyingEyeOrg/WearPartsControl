using CommunityToolkit.Mvvm.ComponentModel;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels;

public sealed class NeedLoginViewModel : ObservableObject
{
    public string Title => LocalizedText.Get("ViewModels.NeedLoginVm.Title");

    public string Description => LocalizedText.Get("ViewModels.NeedLoginVm.Description");

    public string Hint => LocalizedText.Get("ViewModels.NeedLoginVm.Hint");
}