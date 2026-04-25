using Autofac;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public static class EntityFrameworkCoreServiceRegistration
{
    public static void RegisterServices(ContainerBuilder builder)
    {
        builder.Register(ctx => new WearPartsControlDbContextFactory(ctx.Resolve<IServiceProvider>()))
            .AsSelf()
            .As<IDbContextFactory<WearPartsControlDbContext>>()
            .SingleInstance();

        builder.Register(ctx => ctx.Resolve<WearPartsControlDbContextFactory>().CreateDbContext())
            .As<WearPartsControlDbContext>()
            .As<DbContextBase>()
            .InstancePerDependency();

        builder.Register(ctx => new EfUnitOfWork<WearPartsControlDbContext>(ctx.Resolve<WearPartsControlDbContext>()))
            .As<IUnitOfWork<WearPartsControlDbContext>>()
            .As<WearPartsControl.Domain.Repositories.IUnitOfWork>()
            .InstancePerDependency();

        builder.RegisterType<ClientAppConfigurationRepository>()
            .As<IClientAppConfigurationRepository>()
            .InstancePerDependency();

        builder.RegisterType<WearPartRepository>()
            .As<IWearPartRepository>()
            .InstancePerDependency();

        builder.RegisterType<WearPartTypeRepository>()
            .As<IWearPartTypeRepository>()
            .InstancePerDependency();

        builder.RegisterType<ToolChangeRepository>()
            .As<IToolChangeRepository>()
            .InstancePerDependency();

        builder.RegisterType<WearPartReplacementRecordRepository>()
            .As<IWearPartReplacementRecordRepository>()
            .InstancePerDependency();

        builder.RegisterType<ExceedLimitRecordRepository>()
            .As<IExceedLimitRecordRepository>()
            .InstancePerDependency();

        builder.RegisterType<SqliteDatabaseInitializer>()
            .As<IDatabaseInitializer>()
            .SingleInstance();
    }
}
