using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.Exceptions;
using WearPartsControl.Views;

namespace WearPartsControl;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private ILocalizationService? _localizationService;

    public App()
    {
        ConfigureLogging();
        // Setup global exception handling
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            Log.Fatal(exception, "Unhandled exception");
            HandleFriendlyException(exception);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception");
            HandleFriendlyException(args.Exception);
            args.Handled = true; // Prevent app from crashing
        };
    }

    private void HandleFriendlyException(Exception exception)
    {
        var title = _localizationService?["FriendlyErrorTitle"] ?? "提示";

        if (exception is UserFriendlyException userFriendlyException)
        {
            MessageBox.Show(userFriendlyException.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (exception is BusinessException businessException)
        {
            MessageBox.Show(businessException.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    ServiceRegistration.RegisterServices(builder);
                })
                .UseSerilog(Log.Logger, dispose: true)
                .Build();

            _host.StartAsync().GetAwaiter().GetResult();

            var saveInfoStore = _host.Services.GetRequiredService<ISaveInfoStore>();
            SaveInfo.SetStore(saveInfoStore);

            _localizationService = _host.Services.GetRequiredService<ILocalizationService>();
            _localizationService.InitializeAsync().GetAwaiter().GetResult();

            var databaseInitializer = _host.Services.GetRequiredService<IDatabaseInitializer>();
            databaseInitializer.InitializeAsync().GetAwaiter().GetResult();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        if (_host is not null)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                _host.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping host");
            }
        }

        Log.CloseAndFlush();
    }
}

