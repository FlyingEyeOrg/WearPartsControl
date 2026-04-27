using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ViewModels;

public sealed class InputModeOption : ObservableObject
{
    private string _displayName;

    public InputModeOption(string code, string displayName)
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