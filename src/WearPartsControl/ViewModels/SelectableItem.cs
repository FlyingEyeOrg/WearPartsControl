using CommunityToolkit.Mvvm.ComponentModel;

namespace WearPartsControl.ViewModels;

public sealed class SelectableItem<T> : ObservableObject
    where T : class
{
    private bool _isChecked;

    public SelectableItem(T item)
    {
        Item = item;
    }

    public T Item { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}