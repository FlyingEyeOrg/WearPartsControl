using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WearPartsControl.ViewModels;

namespace WearPartsControl.Views
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : AppDialogWindow
    {
        private readonly LoginWindowViewModel _viewModel;
        private DateTimeOffset? _lastInputAt;

        public LoginWindow(LoginWindowViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            Loaded += OnLoaded;
            Closed += OnClosed;
            _viewModel.RequestClose += OnRequestClose;
            _viewModel.RequestClearInput += OnRequestClearInput;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
            PasswordInputBox.Focus();
            Keyboard.Focus(PasswordInputBox);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.RequestClearInput -= OnRequestClearInput;
            Loaded -= OnLoaded;
            Closed -= OnClosed;
        }

        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void OnRequestClearInput(object? sender, EventArgs e)
        {
            PasswordInputBox.Clear();
            _lastInputAt = null;
            PasswordInputBox.Focus();
            Keyboard.Focus(PasswordInputBox);
        }

        private void PasswordInputBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.AuthId = PasswordInputBox.Password;
        }

        private async void PasswordInputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            if (_viewModel.LoginCommand.CanExecute(null))
            {
                await _viewModel.LoginCommand.ExecuteAsync(null);
            }
        }

        private void PasswordInputBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var interval = TimeSpan.FromMilliseconds(_viewModel.LoginInputMaxIntervalMilliseconds);
            if (_viewModel.RequiresCardScan
                && _lastInputAt.HasValue
                && PasswordInputBox.Password.Length > 0
                && now - _lastInputAt.Value > interval)
            {
                PasswordInputBox.Clear();
                _lastInputAt = null;
                _viewModel.RejectManualInput();
                e.Handled = true;
                return;
            }

            _lastInputAt = now;
        }

        private void PasswordInputBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!_viewModel.RequiresCardScan)
            {
                return;
            }

            e.CancelCommand();
            PasswordInputBox.Clear();
            _lastInputAt = null;
            _viewModel.RejectManualInput();
        }
    }
}
