using System.ComponentModel;

namespace WearPartsControl.ApplicationServices.Localization;

public sealed class LocalizationBindingSource : INotifyPropertyChanged
{
    public static LocalizationBindingSource Instance { get; } = new();

    private LocalizationBindingSource()
    {
    }

    public string this[string key] => LocalizedText.Get(key);

    public event EventHandler? Refreshed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        Refreshed?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}