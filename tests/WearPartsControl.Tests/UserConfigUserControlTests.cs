using System;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class UserConfigUserControlTests
{
    [Fact]
    public void UserConfigUserControl_ShouldLoadAndKeepLanguageSelectionAfterSave()
    {
        RunWpfTest(() =>
        {
            using var host = UserConfigUserControlHost.Create(CreateViewModel());

            Assert.NotNull(host.Control);
            Assert.Equal(2, host.LanguageComboBox.Items.Count);

            host.ViewModel.SelectedLanguage = "en-US";
            host.ViewModel.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            host.DrainDispatcher();

            Assert.Equal("en-US", host.ViewModel.SelectedLanguage);
            Assert.Equal(2, host.LanguageComboBox.Items.Count);
            Assert.NotNull(host.LanguageComboBox.SelectedItem);
            Assert.Equal("en-US", ((UserConfigViewModel.LanguageOption)host.LanguageComboBox.SelectedItem!).Code);
            Assert.Equal(new[] { "zh-CN", "en-US" }, host.ViewModel.LanguageOptions.Select(static option => option.Code).ToArray());
            Assert.Equal(new[] { "Simplified Chinese", "English" }, host.ViewModel.LanguageOptions.Select(static option => option.DisplayName).ToArray());
        });
    }

    private static UserConfigViewModel CreateViewModel()
    {
        return new UserConfigViewModel(
            new StubClientAppInfoService(),
            new StubUserConfigService(),
            new StubComNotificationService(),
            new MutableLocalizationService("zh-CN"),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero));
    }

    private static void RunWpfTest(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            var ownsApplication = false;

            try
            {
                ownsApplication = EnsureApplicationResources();
                action();
                DrainDispatcher();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (ownsApplication && Application.Current is not null)
                {
                    Application.Current.Shutdown();
                }

                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static bool EnsureApplicationResources()
    {
        var ownsApplication = false;

        if (Application.Current is null)
        {
            _ = new Application();
            ownsApplication = true;
        }

        var resources = Application.Current!.Resources;
        if (resources.Contains("ButtonPrimary"))
        {
            return ownsApplication;
        }

        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml", UriKind.Absolute)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute)
        });
        resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        return ownsApplication;
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
        {
            frame.Continue = false;
            return null;
        }), null);
        Dispatcher.PushFrame(frame);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            var result = FindDescendant<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class UserConfigUserControlHost : IDisposable
    {
        private readonly Window _window;

        private UserConfigUserControlHost(Window window, UserConfigUserControl control, UserConfigViewModel viewModel, ComboBox languageComboBox)
        {
            _window = window;
            Control = control;
            ViewModel = viewModel;
            LanguageComboBox = languageComboBox;
        }

        public UserConfigUserControl Control { get; }

        public UserConfigViewModel ViewModel { get; }

        public ComboBox LanguageComboBox { get; }

        public static UserConfigUserControlHost Create(UserConfigViewModel viewModel)
        {
            var control = new UserConfigUserControl(viewModel);
            var window = new Window
            {
                Width = 800,
                Height = 600,
                Content = control
            };

            window.Show();
            UserConfigUserControlTests.DrainDispatcher();
            window.UpdateLayout();
            UserConfigUserControlTests.DrainDispatcher();

            var languageComboBox = FindDescendant<ComboBox>(control);
            Assert.NotNull(languageComboBox);

            return new UserConfigUserControlHost(window, control, viewModel, languageComboBox!);
        }

        public void DrainDispatcher()
        {
            UserConfigUserControlTests.DrainDispatcher();
        }

        public void Dispose()
        {
            _window.Close();
            DrainDispatcher();
        }
    }

    private sealed class MutableLocalizationService : ILocalizationService
    {
        public MutableLocalizationService(string cultureName)
        {
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
        }

        public string this[string key] => LocalizedText.Get(key);

        public LocalizationCatalog Catalog => new(LocalizedText.Get);

        public CultureInfo CurrentCulture { get; private set; }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
            return ValueTask.CompletedTask;
        }

        private static void ApplyCulture(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel());
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUserConfigService : IUserConfigService
    {
        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new UserConfig());
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubComNotificationService : IComNotificationService
    {
        public ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }
}
