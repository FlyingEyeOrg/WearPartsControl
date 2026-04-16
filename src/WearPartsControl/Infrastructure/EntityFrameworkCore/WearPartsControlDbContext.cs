using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class WearPartsControlDbContext : DbContextBase
{
    public WearPartsControlDbContext(DbContextOptions<WearPartsControlDbContext> options) : base(options)
    {
    }

    public DbSet<BasicConfigurationEntity> BasicConfigurations => Set<BasicConfigurationEntity>();

    public DbSet<WearPartDefinitionEntity> WearPartDefinitions => Set<WearPartDefinitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WearPartsControlDbContext).Assembly);
    }
}
