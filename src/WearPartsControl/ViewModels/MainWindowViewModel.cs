using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel
    {
        public string Title { get; set; }

        public MainWindowViewModel(ILocalizationService localizationService)
        {
            Title = localizationService["MainWindow.Title"];
        }

        public IEnumerable<string> Values { get; set; } = new List<string>()
        {
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "易损件更换",
            "设备基础信息"
        };
    }
}