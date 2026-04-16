using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.Infrastructure;

public sealed class WearPartsControlDbContext : DbContextBase
{
    public WearPartsControlDbContext(DbContextOptions<WearPartsControlDbContext> options) : base(options)
    {
    }

    public DbSet<BasicConfigurationEntity> BasicConfigurations => Set<BasicConfigurationEntity>();

    public DbSet<WearPartDefinitionEntity> WearPartDefinitions => Set<WearPartDefinitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BasicConfigurationEntity>(entity =>
        {
            entity.ToTable("basic_configurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SiteCode).HasMaxLength(64);
            entity.Property(x => x.FactoryCode).HasMaxLength(64);
            entity.Property(x => x.AreaCode).HasMaxLength(64);
            entity.Property(x => x.ProcedureCode).HasMaxLength(64);
            entity.Property(x => x.EquipmentCode).HasMaxLength(64);
            entity.Property(x => x.ResourceNumber).HasMaxLength(128);
            entity.HasIndex(x => x.ResourceNumber);
        });

        modelBuilder.Entity<WearPartDefinitionEntity>(entity =>
        {
            entity.ToTable("wear_part_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceNumber).HasMaxLength(128);
            entity.Property(x => x.PartName).HasMaxLength(128);
            entity.Property(x => x.CurrentValueAddress).HasMaxLength(128);
            entity.Property(x => x.WarningValueAddress).HasMaxLength(128);
            entity.Property(x => x.ShutdownValueAddress).HasMaxLength(128);

            entity.HasOne(x => x.BasicConfiguration)
                .WithMany(x => x.WearPartDefinitions)
                .HasForeignKey(x => x.BasicConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.BasicConfigurationId, x.PartName });
        });
    }
}
