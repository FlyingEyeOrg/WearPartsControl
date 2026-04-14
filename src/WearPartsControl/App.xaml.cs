using System;
using System.Windows;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WearPartsControl;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    public App()
    {
        ConfigureLogging();
        // Setup global exception handling
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            Log.Fatal(exception, "Unhandled exception");
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception");
            args.Handled = true; // Prevent app from crashing
        };
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

