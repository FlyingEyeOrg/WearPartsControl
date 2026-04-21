using Autofac;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.LoginService;
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
        // Register WPF windows and application services here
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<TypeJsonSaveInfoStore>().As<ISaveInfoStore>().SingleInstance();
        builder.RegisterType<LocalizationService>().As<ILocalizationService>().SingleInstance();
        builder.RegisterType<ExceptionToStatusCodeMapper>().As<WearPartsControl.Exceptions.IExceptionToStatusCodeMapper>().SingleInstance();
        builder.Register(_ =>
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return client;
            })
            .SingleInstance();
        builder.RegisterType<HttpJsonService>().As<IHttpJsonService>().SingleInstance();
        builder.RegisterType<UiDispatcher>().As<IUiDispatcher>().SingleInstance();
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
        builder.RegisterType<PlcService>().As<IPlcService>().SingleInstance();
        builder.RegisterType<PlcConnectionStatusService>().As<IPlcConnectionStatusService>().SingleInstance();
        builder.RegisterType<PlcStartupConnectionService>().As<IPlcStartupConnectionService>().InstancePerDependency();
        builder.RegisterType<AppSettingsService>().As<IAppSettingsService>().SingleInstance();
        builder.RegisterType<UiBusyService>().As<IUiBusyService>().SingleInstance();
        builder.RegisterType<JsonClientAppInfoSelectionOptionsProvider>().As<IClientAppInfoSelectionOptionsProvider>().SingleInstance();
        builder.RegisterType<ClientAppInfoService>().As<IClientAppInfoService>().InstancePerLifetimeScope();
        builder.RegisterType<LegacyDatabaseImportService>().As<ILegacyDatabaseImportService>().SingleInstance();
        builder.RegisterType<WearPartManagementService>().As<IWearPartManagementService>().InstancePerLifetimeScope();
        builder.RegisterType<WearPartReplacementService>().As<IWearPartReplacementService>().InstancePerLifetimeScope();
        builder.RegisterType<BarcodeLengthReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<BarcodeReuseReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<LifetimeReachedReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<ChangePositionReplacementGuard>().As<IWearPartReplacementGuard>().InstancePerDependency();
        builder.RegisterType<WearPartMonitorService>().As<IWearPartMonitorService>().InstancePerLifetimeScope();
        builder.RegisterType<WearPartMonitoringHostedService>().As<IHostedService>().SingleInstance();
        EntityFrameworkCoreServiceRegistration.RegisterServices(builder);
        builder.RegisterType<WearPartDefinitionDomainService>().AsSelf().SingleInstance();
        builder.RegisterType<PartManagementViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<AddPartWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<EditPartWindowViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ClientAppInfoViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<ReplacePartViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<UserConfigViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<MainWindowViewModel>().AsSelf().InstancePerDependency();
        // Add other services as needed

        builder.RegisterType<AddPartWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<EditPartWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<ReplacePartUserControl>().AsSelf();
        builder.RegisterType<ClientAppInfoUserControl>().AsSelf();
        builder.RegisterType<PartInfoUserControl>().AsSelf();
        builder.RegisterType<UserConfigUserControl>().AsSelf();
        builder.RegisterType<PartManagementUserControl>().AsSelf();
        builder.RegisterType<PartUpdateRecordUserControl>().AsSelf();
        builder.RegisterType<LoginWindowViewModel>().AsSelf();
        builder.RegisterType<LoginWindow>();
    }
}