using Autofac;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.LoginService;
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
        builder.RegisterType<CurrentUserAccessor>()
            .As<ICurrentUserAccessor>()
            .As<ICurrentUser>()
            .SingleInstance();
        builder.RegisterType<MhrUserDirectoryCache>().As<IMhrUserDirectoryCache>().SingleInstance();
        builder.RegisterType<LoginService>().As<ILoginService>().SingleInstance();
        builder.RegisterType<ComNotificationService>().As<IComNotificationService>().SingleInstance();
        builder.RegisterType<SpacerManagementService>().As<ISpacerManagementService>().SingleInstance();
        builder.RegisterType<PlcService>().As<IPlcService>().SingleInstance();
        builder.RegisterType<AppSettingsService>().As<IAppSettingsService>().SingleInstance();
        builder.RegisterType<LegacyDatabaseImportService>().As<ILegacyDatabaseImportService>().SingleInstance();
        builder.RegisterType<WearPartManagementService>().As<IWearPartManagementService>().InstancePerLifetimeScope();
        builder.RegisterType<WearPartReplacementService>().As<IWearPartReplacementService>().InstancePerLifetimeScope();
        builder.RegisterType<WearPartMonitorService>().As<IWearPartMonitorService>().InstancePerLifetimeScope();
        builder.RegisterType<WearPartMonitoringHostedService>().As<IHostedService>().SingleInstance();
        EntityFrameworkCoreServiceRegistration.RegisterServices(builder);
        builder.RegisterType<WearPartDefinitionDomainService>().AsSelf().SingleInstance();
        builder.RegisterType<MainWindowViewModel>().AsSelf().InstancePerDependency();
        // Add other services as needed

        builder.RegisterType<ReplacePartUserControl>().AsSelf();
        builder.RegisterType<ClientAppInfoUserControl>().AsSelf();
        builder.RegisterType<UserConfigUserControl>().AsSelf();
        builder.RegisterType<PartManagementUserControl>().AsSelf();
        builder.RegisterType<PartUpdateRecordUserControl>().AsSelf();
        builder.RegisterType<LoginWindowViewModel>().AsSelf();
        builder.RegisterType<LoginWindow>();
    }
}