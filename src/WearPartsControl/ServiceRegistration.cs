using Autofac;
using System;
using System.Net.Http;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl;

public static class ServiceRegistration
{
    public static void RegisterServices(ContainerBuilder builder)
    {
        // Register WPF windows and application services here
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<TypeJsonSaveInfoStore>().As<ISaveInfoStore>().SingleInstance();
        builder.Register(_ =>
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return client;
            })
            .SingleInstance();
        builder.RegisterType<HttpJsonClient>().As<IHttpJsonClient>().SingleInstance();
        // Add other services as needed
    }
}