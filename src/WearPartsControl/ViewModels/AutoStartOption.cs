using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ViewModels;

public sealed class AutoStartOption : ObservableObject
{
    private string _displayName;

    public AutoStartOption(bool isEnabled, string displayName)
    {
        IsEnabled = isEnabled;
        _displayName = displayName;
    }

    public bool IsEnabled { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}