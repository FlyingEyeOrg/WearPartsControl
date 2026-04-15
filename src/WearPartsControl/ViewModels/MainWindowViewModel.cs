using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        private int _selectedTabIndex;
        private string? _selectedTabHeader;

        public MainWindowViewModel(ILocalizationService localizationService)
        {
            Title = localizationService["MainWindow.Title"];
            Values = localizationService.Catalog.MainWindow.Tabs;
            TabChangedCommand = new RelayCommand<string?>(OnTabChanged);
            _selectedTabIndex = -1;
        }

        public string Title { get; set; }

        public IEnumerable<string> Values { get; }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex == value)
                {
                    return;
                }

                _selectedTabIndex = value;
                OnPropertyChanged();
            }
        }

        public string? SelectedTabHeader
        {
            get => _selectedTabHeader;
            private set
            {
                if (_selectedTabHeader == value)
                {
                    return;
                }

                _selectedTabHeader = value;
                OnPropertyChanged();
            }
        }

        public ICommand TabChangedCommand { get; }

        private void OnTabChanged(string? header)
        {
            SelectedTabHeader = header;
        }
    }
}