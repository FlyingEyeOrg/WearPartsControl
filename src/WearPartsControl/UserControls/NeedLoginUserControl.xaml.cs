using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// NeedLoginUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class NeedLoginUserControl : UserControl
    {
        public NeedLoginUserControl(NeedLoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
