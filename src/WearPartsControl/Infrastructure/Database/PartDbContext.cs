using Microsoft.EntityFrameworkCore;

namespace WearPartsControl.Infrastructure.Database;

public sealed class PartDbContext : DbContext
{
    public PartDbContext(DbContextOptions<PartDbContext> options) : base(options)
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
            entity.Property(x => x.ResourceNumber).HasMaxLength(128);
            entity.HasIndex(x => x.ResourceNumber);
            entity.Property(x => x.SiteCode).HasMaxLength(64);
            entity.Property(x => x.FactoryCode).HasMaxLength(64);
            entity.Property(x => x.AreaCode).HasMaxLength(64);
            entity.Property(x => x.ProcedureCode).HasMaxLength(64);
        });

        modelBuilder.Entity<WearPartDefinitionEntity>(entity =>
        {
            entity.ToTable("wear_part_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceNumber).HasMaxLength(128);
            entity.Property(x => x.PartName).HasMaxLength(128);
            entity.HasIndex(x => new { x.BasicConfigurationId, x.PartName });

            entity
                .HasOne(x => x.BasicConfiguration)
                .WithMany(x => x.WearPartDefinitions)
                .HasForeignKey(x => x.BasicConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
