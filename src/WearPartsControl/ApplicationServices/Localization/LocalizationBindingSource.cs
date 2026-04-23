using System.ComponentModel;

namespace WearPartsControl.ApplicationServices.Localization;

public sealed class LocalizationBindingSource : INotifyPropertyChanged
{
    public static LocalizationBindingSource Instance { get; } = new();

    private LocalizationBindingSource()
    {
    }

    public string this[string key] => LocalizedText.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}