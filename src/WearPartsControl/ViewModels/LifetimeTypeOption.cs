using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ViewModels;

public sealed class LifetimeTypeOption : ObservableObject
{
    private string _displayName;

    public LifetimeTypeOption(string code, string displayName)
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