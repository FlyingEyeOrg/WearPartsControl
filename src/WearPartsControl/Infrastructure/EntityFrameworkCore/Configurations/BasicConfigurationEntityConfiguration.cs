using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Configurations;

public sealed class BasicConfigurationEntityConfiguration : IEntityTypeConfiguration<BasicConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<BasicConfigurationEntity> builder)
    {
        builder.ToTable("basic_configurations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(512);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.SiteCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FactoryCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AreaCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProcedureCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EquipmentCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResourceNumber).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PlcProtocolType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PlcIpAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ShutdownPointAddress).HasMaxLength(128);

        builder.HasIndex(x => x.ResourceNumber).IsUnique();
    }
}
