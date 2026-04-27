using Autofac;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.Shell;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Domain.Services;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.Exceptions;
using WearPartsControl.Views;
using WearPartsControl.ViewModels;
using WearPartsControl.UserControls;

namespace WearPartsControl;

public static class ServiceRegistration
{
    public static void RegisterServices(ContainerBuilder builder)
    {
        RegisterShell(builder);
        RegisterInfrastructure(builder);
        RegisterCoreApplicationServices(builder);
        RegisterMonitoringServices(builder);
        RegisterViewModels(builder);
        RegisterViews(builder);
        RegisterUserControls(builder);
    }

    private static void RegisterShell(ContainerBuilder builder)
    {
        builder.RegisterType<AppDialogService>().As<IAppDialogService>().SingleInstance();
        builder.RegisterType<FileDialogService>().As<IFileDialogService>().SingleInstance();
        builder.RegisterType<MainWindowNavigationService>().As<IMainWindowNavigationService>().SingleInstance();
        builder.RegisterType<MainWindowContentFactory>().As<IMainWindowContentFactory>().SingleInstance();
        builder.RegisterType<MainWindowViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<MainWindow>().AsSelf().SingleInstance();
        builder.RegisterType<LoginWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<LoginWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<UiDispatcher>().As<IUiDispatcher>().SingleInstance();
        builder.RegisterType<UiBusyService>().As<IUiBusyService>().SingleInstance();
        builder.RegisterType<AutoLogoutInteractionService>().As<IAutoLogoutInteractionService>().SingleInstance();
    }

    private static void RegisterInfrastructure(ContainerBuilder builder)
    {
        builder.RegisterType<TypeJsonSaveInfoStore>().As<ISaveInfoStore>().SingleInstance();
        builder.RegisterType<AppStartupCoordinator>().As<IAppStartupCoordinator>().SingleInstance();
        builder.RegisterType<StartupPlcWarmupService>().As<IStartupPlcWarmupService>().SingleInstance();
        builder.RegisterType<LocalizationService>().As<ILocalizationService>().SingleInstance();
        builder.RegisterType<ExceptionToStatusCodeMapper>().As<WearPartsControl.Exceptions.IExceptionToStatusCodeMapper>().SingleInstance();
        builder.Register(_ => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        })
            .SingleInstance();
        builder.Register(_ =>
            {
                var handler = _.Resolve<SocketsHttpHandler>();
                var client = new HttpClient(handler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return client;
            })
            .SingleInstance();
        builder.RegisterType<HttpRequestService>().As<IHttpRequestService>().SingleInstance();
        builder.RegisterType<HttpJsonService>().As<IHttpJsonService>().SingleInstance();
        EntityFrameworkCoreServiceRegistration.RegisterServices(builder);
    }

    private static void RegisterCoreApplicationServices(ContainerBuilder builder)
    {
        builder.RegisterType<CurrentUserAccessor>()
            .As<ICurrentUserAccessor>()
            .As<ICurrentUser>()
            .SingleInstance();
        builder.RegisterType<MhrUserDirectoryCache>().As<IMhrUserDirectoryCache>().SingleInstance();
        builder.RegisterType<LoginService>().As<ILoginService>().SingleInstance();
        builder.RegisterType<LoginSessionStateMachine>().As<ILoginSessionStateMachine>().SingleInstance();
        builder.RegisterType<UserConfigService>().As<IUserConfigService>().SingleInstance();
        builder.RegisterType<ComNotificationService>().As<IComNotificationService>().SingleInstance();
        builder.RegisterType<SpacerManagementService>().As<ISpacerManagementService>().SingleInstance();
        builder.RegisterType<AppSettingsService>().As<IAppSettingsService>().SingleInstance();
        builder.RegisterType<MonitoringRuntimeStateProvider>().As<IMonitoringRuntimeStateProvider>().SingleInstance();
        builder.RegisterType<PlcService>().AsSelf().SingleInstance();
        builder.RegisterType<PlcClientConfigurationResolver>().As<IPlcClientConfigurationResolver>().SingleInstance();
        builder.RegisterType<PlcConnectionTestService>().As<IPlcConnectionTestService>().SingleInstance();
        builder.Register(_ => new PlcOperationPipeline(
                _.Resolve<PlcService>(),
            _.Resolve<Microsoft.Extensions.Logging.ILogger<PlcOperationPipeline>>(),
            _.Resolve<IAppSettingsService>()))
            .As<IPlcOperationPipeline>()
            .SingleInstance();
        builder.RegisterType<PlcConnectionStatusService>().As<IPlcConnectionStatusService>().SingleInstance();
        builder.RegisterType<PlcStartupConnectionService>().As<IPlcStartupConnectionService>().InstancePerDependency();
        builder.RegisterType<JsonClientAppInfoSelectionOptionsProvider>().As<IClientAppInfoSelectionOptionsProvider>().SingleInstance();
        builder.RegisterType<ClientAppInfoService>().As<IClientAppInfoService>().InstancePerDependency();
        builder.RegisterType<LegacyDatabaseImportService>().As<ILegacyDatabaseImportService>().SingleInstance();
        builder.RegisterType<LegacyConfigurationImportService>().As<ILegacyConfigurationImportService>().SingleInstance();
        builder.RegisterType<WearPartManagementService>().As<IWearPartManagementService>().InstancePerDependency();
        builder.RegisterType<WearPartTypeService>().As<IWearPartTypeService>().InstancePerDependency();
        builder.RegisterType<ToolChangeManagementService>().As<IToolChangeManagementService>().InstancePerDependency();
        builder.RegisterType<CutterMesValidationService>().As<ICutterMesValidationService>().InstancePerDependency();
        builder.RegisterType<WearPartReplacementService>().As<IWearPartReplacementService>().InstancePerDependency();
        builder.RegisterType<ToolChangeSelectionService>().As<IToolChangeSelectionService>().SingleInstance();
        builder.RegisterType<BarcodeLengthReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<CutterRollValidationReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<CutterMesConsistencyReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<ToolCodeReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<CoatingSpacerReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<BarcodeReuseReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<LifetimeReachedReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<ChangePositionReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<WearPartDefinitionDomainService>().AsSelf().SingleInstance();
    }

    private static void RegisterMonitoringServices(ContainerBuilder builder)
    {
        builder.RegisterType<PlcConfigurationMonitorService>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<WearPartMonitorService>().As<IWearPartMonitorService>().InstancePerDependency();
        builder.RegisterType<WearPartMonitoringHostedService>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<WearPartMonitoringControlService>().As<IWearPartMonitoringControlService>().SingleInstance();
    }

    private static void RegisterViewModels(ContainerBuilder builder)
    {
        builder.RegisterType<PartManagementViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<AddPartWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<EditPartWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ClientAppInfoViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ReplacePartViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ToolChangeManagementViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<PartReplacementHistoryViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<NeedLoginViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<UserConfigViewModel>().AsSelf().InstancePerDependency();
    }

    private static void RegisterViews(ContainerBuilder builder)
    {
        builder.RegisterType<AddPartWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<EditPartWindow>().AsSelf().InstancePerDependency();
    }

    private static void RegisterUserControls(ContainerBuilder builder)
    {
        builder.RegisterType<ReplacePartUserControl>().AsSelf();
        builder.RegisterType<ClientAppInfoUserControl>().AsSelf();
        builder.RegisterType<PartInfoUserControl>().AsSelf();
        builder.RegisterType<NeedLoginUserControl>().AsSelf();
        builder.RegisterType<UserConfigUserControl>().AsSelf();
        builder.RegisterType<PartManagementUserControl>().AsSelf();
        builder.RegisterType<ToolChangeManagementUserControl>().AsSelf();
        builder.RegisterType<PartReplacementHistoryUserControl>().AsSelf();
    }
}