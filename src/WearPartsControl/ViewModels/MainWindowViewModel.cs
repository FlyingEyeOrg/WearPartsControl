using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            TabChangedCommand = new RelayCommand<string?>(OnTabChanged);
            _selectedTabIndex = 10;
        }

        public string Title { get; set; }

        public IEnumerable<string> Values { get; set; } = new List<string>()
        {
            "易损件更换",
            "设备基础信息",
            "易损件管理",
            "易损件更换历史",
            "用户配置"
        };

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