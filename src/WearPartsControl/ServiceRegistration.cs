using Autofac;
using Autofac.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl;

public static class ServiceRegistration
{
    public static void RegisterServices(ContainerBuilder builder)
    {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Populate(services);

        // Register WPF windows and application services here
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<TypeJsonSaveInfoStore>().As<ISaveInfoStore>().SingleInstance();
        builder.RegisterType<LocalizationService>().As<ILocalizationService>().SingleInstance();
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
        // Add other services as needed
    }
}