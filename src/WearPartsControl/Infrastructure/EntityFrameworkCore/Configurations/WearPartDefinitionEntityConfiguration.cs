using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Configurations;

public sealed class WearPartDefinitionEntityConfiguration : IEntityTypeConfiguration<WearPartDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<WearPartDefinitionEntity> builder)
    {
        builder.ToTable("wear_part_definitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();

        builder.Property(x => x.ResourceNumber).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PartName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.InputMode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CurrentValueAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CurrentValueDataType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WarningValueAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.WarningValueDataType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ShutdownValueAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ShutdownValueDataType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsShutdown).IsRequired();
        builder.Property(x => x.CodeMinLength).IsRequired();
        builder.Property(x => x.CodeMaxLength).IsRequired();
        builder.Property(x => x.LifetimeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ToolChangeId);
        builder.Property(x => x.PlcZeroClearAddress).HasMaxLength(128);
        builder.Property(x => x.BarcodeWriteAddress).HasMaxLength(128).IsRequired();

        builder.HasOne(x => x.ClientAppConfiguration)
            .WithMany(x => x.WearPartDefinitions)
            .HasForeignKey(x => x.ClientAppConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ToolChange)
            .WithMany(x => x.WearPartDefinitions)
            .HasForeignKey(x => x.ToolChangeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ClientAppConfigurationId, x.PartName }).IsUnique();
    }
}
