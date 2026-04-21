using Autofac;
using Autofac.Extensions.DependencyInjection;
using HslCommunication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.Startup;
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
    private IAppStartupCoordinator? _appStartupCoordinator;
    private Task? _hostStartTask;
    private CancellationTokenSource? _startupCancellationTokenSource;

    public App()
    {
        ConfigureLogging();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void HandleFriendlyException(Exception exception)
    {
        var title = GetLocalizedText("FriendlyErrorTitle");

        if (exception is UserFriendlyException userFriendlyException)
        {
            MessageBox.Show(userFriendlyException.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (exception is BusinessException businessException)
        {
            MessageBox.Show(businessException.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(GetLocalizedText("UnexpectedError"), title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void ConfigureLogging()
    {
        var logsDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
        System.IO.Directory.CreateDirectory(logsDirectory);
        var logFilePath = System.IO.Path.Combine(logsDirectory, "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Authorization.SetAuthorizationCode("7525828d-68c9-4d31-b6db-e5162b91ef7b");

            _host = BuildHost();

            var saveInfoStore = _host.Services.GetRequiredService<ISaveInfoStore>();
            SaveInfo.SetStore(saveInfoStore);

            _localizationService = _host.Services.GetRequiredService<ILocalizationService>();
            await _localizationService.InitializeAsync().ConfigureAwait(true);
            _appStartupCoordinator = _host.Services.GetRequiredService<IAppStartupCoordinator>();

            var legacyDatabasePath = LegacyImportCommandLine.GetLegacyDatabasePathOrDefault(e.Args);
            if (!string.IsNullOrWhiteSpace(legacyDatabasePath))
            {
                await RunLegacyImportAsync(legacyDatabasePath).ConfigureAwait(true);
                return;
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            _startupCancellationTokenSource = new CancellationTokenSource();
            _hostStartTask = StartHostAsync(_host, _startupCancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            HandleFriendlyException(ex);
            Shutdown();
        }
    }

    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                ServiceRegistration.RegisterServices(builder);
            })
            .UseSerilog(Log.Logger, dispose: true)
            .Build();
    }

    private async Task RunLegacyImportAsync(string legacyDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDatabasePath);

        if (_host is null || _appStartupCoordinator is null)
        {
            throw new InvalidOperationException("Application host is not initialized.");
        }

        await _appStartupCoordinator.EnsureInitializedAsync().ConfigureAwait(true);
        var importService = _host.Services.GetRequiredService<ILegacyDatabaseImportService>();
        var importResult = await importService.ImportAsync(legacyDatabasePath).ConfigureAwait(true);
        MessageBox.Show(importResult.ToSummary(), GetLocalizedText("App.LegacyImportCompletedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        Shutdown();
    }

    private async Task StartHostAsync(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            if (_appStartupCoordinator is not null)
            {
                await _appStartupCoordinator.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            }

            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly during startup");
            Dispatcher.Invoke(() =>
            {
                HandleFriendlyException(ex);
                Shutdown();
            });
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is not Exception exception)
        {
            return;
        }

        Log.Fatal(exception, "Unhandled exception");
        HandleFriendlyException(exception);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Error(args.Exception, "Dispatcher unhandled exception");
        HandleFriendlyException(args.Exception);
        args.Handled = true;
    }

    private string GetLocalizedText(string key)
    {
        return _localizationService?[key] ?? LocalizedText.Get(key);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        _startupCancellationTokenSource?.Cancel();

        if (_hostStartTask is not null)
        {
            try
            {
                _hostStartTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error awaiting host startup task");
            }
        }

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

        _startupCancellationTokenSource?.Dispose();
        _startupCancellationTokenSource = null;

        Log.CloseAndFlush();
    }
}

