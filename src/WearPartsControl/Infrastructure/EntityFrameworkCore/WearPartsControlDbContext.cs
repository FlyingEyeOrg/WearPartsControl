using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class WearPartsControlDbContext : DbContextBase
{
    public WearPartsControlDbContext(
        DbContextOptions<WearPartsControlDbContext> options,
        IServiceProvider? applicationServiceProvider = null)
        : base(options, applicationServiceProvider)
    {
    }

    public DbSet<ClientAppConfigurationEntity> ClientAppConfigurations => Set<ClientAppConfigurationEntity>();

    public DbSet<WearPartDefinitionEntity> WearPartDefinitions => Set<WearPartDefinitionEntity>();

    public DbSet<WearPartTypeEntity> WearPartTypes => Set<WearPartTypeEntity>();

    public DbSet<WearPartReplacementRecordEntity> WearPartReplacementRecords => Set<WearPartReplacementRecordEntity>();

    public DbSet<ExceedLimitRecordEntity> ExceedLimitRecords => Set<ExceedLimitRecordEntity>();

    public DbSet<ToolChangeEntity> ToolChanges => Set<ToolChangeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WearPartsControlDbContext).Assembly);
    }
}
