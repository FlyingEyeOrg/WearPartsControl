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
            .InstancePerLifetimeScope();

        builder.Register(ctx => new EfUnitOfWork<WearPartsControlDbContext>(ctx.Resolve<WearPartsControlDbContext>()))
            .As<IUnitOfWork<WearPartsControlDbContext>>()
            .As<WearPartsControl.Domain.Repositories.IUnitOfWork>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BasicConfigurationRepository>()
            .As<IBasicConfigurationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<WearPartRepository>()
            .As<IWearPartRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<WearPartReplacementRecordRepository>()
            .As<IWearPartReplacementRecordRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ExceedLimitRecordRepository>()
            .As<IExceedLimitRecordRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<SqliteDatabaseInitializer>()
            .As<IDatabaseInitializer>()
            .SingleInstance();
    }
}
