using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel
    {
        private TabPageViewModel? _selectedTab;

        public string Title { get; set; }

        public TabPageViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value)
                {
                    return;
                }

                _selectedTab = value;
            }
        }

        public MainWindowViewModel(ILocalizationService localizationService)
        {
            Title = localizationService["MainWindow.Title"];
        }
    }
}