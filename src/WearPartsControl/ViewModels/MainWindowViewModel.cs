using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.UserControls;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        private string? _selectedTabHeader;
        private readonly IServiceProvider _serviceProvider;

        public MainWindowViewModel(
            ILocalizationService localizationService,
            IServiceProvider serviceProvider)
        {
            Title = localizationService["MainWindow.Title"];
            Tabs = localizationService.Catalog.MainWindow.Tabs;
            TabChangedCommand = new RelayCommand<int>(OnTabChanged);
            _serviceProvider = serviceProvider;
            _selectedContent = _serviceProvider.GetRequiredService<ReplacePartUserControl>();
        }

        public string Title { get; set; }

        private object _selectedContent;

        public object SelectedContent
        {
            get { return _selectedContent; }
            set => SetProperty(ref _selectedContent, value);
        }

        public IEnumerable<string> Tabs { get; }

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

        private void OnTabChanged(int index)
        {
        }
    }
}