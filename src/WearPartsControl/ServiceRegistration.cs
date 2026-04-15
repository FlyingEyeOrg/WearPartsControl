using Autofac;
using System.Net.Http;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Exceptions;
using WearPartsControl.Views;
using WearPartsControl.ViewModels;

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
        builder.RegisterType<LoginService>().As<ILoginService>().SingleInstance();
        builder.RegisterType<MainWindowViewModel>().AsSelf().InstancePerDependency();
        // Add other services as needed
    }
}