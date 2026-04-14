using Microsoft.Extensions.Logging;
using System.Windows;

namespace WearPartsControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(ILogger<MainWindow> logger)
        {
            logger.LogInformation("MainWindow created");
            InitializeComponent();
        }
    }
}

