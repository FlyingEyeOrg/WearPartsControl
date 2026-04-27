using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ViewModels;

public sealed class LanguageOption : ObservableObject
{
    private string _displayName;

    public LanguageOption(string code, string displayName)
    {
        Code = code;
        _displayName = displayName;
    }

    public string Code { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}