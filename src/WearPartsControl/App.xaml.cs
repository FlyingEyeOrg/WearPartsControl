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
    private readonly object _shutdownSyncRoot = new();
    private IHost? _host;
    private ILocalizationService? _localizationService;
    private IAppStartupCoordinator? _appStartupCoordinator;
    private Task? _hostStartTask;
    private Task? _shutdownTask;
    private CancellationTokenSource? _startupCancellationTokenSource;
    private int _shutdownRequested;

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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            StartupPerformanceTracker.Restart("应用启动入口");
            Authorization.SetAuthorizationCode("7525828d-68c9-4d31-b6db-e5162b91ef7b");

            _host = BuildHost();
            StartupPerformanceTracker.Mark("主机构建完成");

            var saveInfoStore = _host.Services.GetRequiredService<ISaveInfoStore>();
            SaveInfo.SetStore(saveInfoStore);

            _localizationService = _host.Services.GetRequiredService<ILocalizationService>();
            await _localizationService.InitializeAsync().ConfigureAwait(true);
            StartupPerformanceTracker.Mark("本地化初始化完成");
            _appStartupCoordinator = _host.Services.GetRequiredService<IAppStartupCoordinator>();

            var legacyDatabasePath = LegacyImportCommandLine.GetLegacyDatabasePathOrDefault(e.Args);
            if (!string.IsNullOrWhiteSpace(legacyDatabasePath))
            {
                await RunLegacyImportAsync(legacyDatabasePath).ConfigureAwait(true);
                return;
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            StartupPerformanceTracker.Mark("主窗口解析完成");
            _startupCancellationTokenSource = new CancellationTokenSource();
            MainWindow = mainWindow;
            mainWindow.Show();
            StartupPerformanceTracker.Mark("主窗口已显示（首屏）");

            await mainWindow.InitializeAsync(_startupCancellationTokenSource.Token).ConfigureAwait(true);
            StartupPerformanceTracker.Mark("主窗口视图模型初始化完成");

            _hostStartTask = StartHostAsync(_host, _startupCancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            HandleFriendlyException(ex);
            await RequestShutdownAsync("启动阶段发生未处理异常").ConfigureAwait(true);
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
            .UseSerilog(Log.Logger, dispose: false)
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
        await RequestShutdownAsync("旧库导入完成后退出").ConfigureAwait(true);
    }

    private async Task StartHostAsync(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            if (_appStartupCoordinator is not null)
            {
                await _appStartupCoordinator.EnsureInitializedAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                StartupPerformanceTracker.Mark("数据库初始化完成");
            }

            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("宿主后台启动完成");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly during startup");
            await Dispatcher.InvokeAsync(() => HandleFriendlyException(ex));
            await RequestShutdownAsync("宿主启动阶段失败", awaitHostStartupTask: false).ConfigureAwait(false);
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
        _ = RequestShutdownAsync("AppDomain 未处理异常");
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Error(args.Exception, "Dispatcher unhandled exception");
        HandleFriendlyException(args.Exception);
        args.Handled = true;
        _ = RequestShutdownAsync("Dispatcher 未处理异常");
    }

    internal bool IsShutdownRequested => Volatile.Read(ref _shutdownRequested) == 1;

    internal Task RequestShutdownAsync(string reason, int exitCode = 0, bool awaitHostStartupTask = true)
    {
        lock (_shutdownSyncRoot)
        {
            _shutdownTask ??= ShutdownCoreAsync(reason, exitCode, awaitHostStartupTask);
            return _shutdownTask;
        }
    }

    private async Task ShutdownCoreAsync(string reason, int exitCode, bool awaitHostStartupTask)
    {
        Interlocked.Exchange(ref _shutdownRequested, 1);
        ShutdownPerformanceTracker.Restart($"收到关停请求: {reason}");

        _startupCancellationTokenSource?.Cancel();
        ShutdownPerformanceTracker.Mark("已取消启动阶段令牌");

        if (awaitHostStartupTask && _hostStartTask is not null)
        {
            try
            {
                await _hostStartTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error awaiting host startup task during shutdown");
            }
            finally
            {
                ShutdownPerformanceTracker.Mark("启动后台任务收尾完成");
            }
        }

        if (_host is not null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                ShutdownPerformanceTracker.Mark("宿主停止完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping host during shutdown");
            }
            finally
            {
                _host.Dispose();
                _host = null;
                ShutdownPerformanceTracker.Mark("宿主释放完成");
            }
        }

        _appStartupCoordinator = null;
        _startupCancellationTokenSource?.Dispose();
        _startupCancellationTokenSource = null;
        ShutdownPerformanceTracker.Mark("应用资源清理完成");

        await Dispatcher.InvokeAsync(() =>
        {
            ShutdownPerformanceTracker.Mark("触发应用退出");
            Shutdown(exitCode);
        });
    }

    private string GetLocalizedText(string key)
    {
        return _localizationService?[key] ?? LocalizedText.Get(key);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownPerformanceTracker.Mark($"Exit 事件开始，退出码 {e.ApplicationExitCode}");
        base.OnExit(e);
        ShutdownPerformanceTracker.Mark("Exit 事件完成，准备刷新日志");
        Log.CloseAndFlush();
    }
}

