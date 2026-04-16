using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Configurations;

public sealed class WearPartReplacementRecordEntityConfiguration : IEntityTypeConfiguration<WearPartReplacementRecordEntity>
{
    public void Configure(EntityTypeBuilder<WearPartReplacementRecordEntity> builder)
    {
        builder.ToTable("wear_part_replacement_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(512);
        builder.Property(x => x.SiteCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PartName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OldBarcode).HasMaxLength(128);
        builder.Property(x => x.NewBarcode).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CurrentValue).HasMaxLength(128).IsRequired();
        builder.Property(x => x.WarningValue).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ShutdownValue).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OperatorWorkNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OperatorUserName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacementReason).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacementMessage).HasMaxLength(512).IsRequired();
        builder.Property(x => x.DataType).HasMaxLength(64);
        builder.Property(x => x.DataValue).HasMaxLength(512);

        builder.HasOne(x => x.BasicConfiguration)
            .WithMany(x => x.WearPartReplacementRecords)
            .HasForeignKey(x => x.BasicConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WearPartDefinition)
            .WithMany(x => x.WearPartReplacementRecords)
            .HasForeignKey(x => x.WearPartDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WearPartDefinitionId, x.ReplacedAt });
        builder.HasIndex(x => new { x.WearPartDefinitionId, x.NewBarcode });
    }
}