using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Configurations;

public sealed class ExceedLimitRecordEntityConfiguration : IEntityTypeConfiguration<ExceedLimitRecordEntity>
{
    public void Configure(EntityTypeBuilder<ExceedLimitRecordEntity> builder)
    {
        builder.ToTable("exceed_limit_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(512);
        builder.Property(x => x.PartName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NotificationMessage).HasMaxLength(512).IsRequired();

        builder.HasOne(x => x.BasicConfiguration)
            .WithMany(x => x.ExceedLimitRecords)
            .HasForeignKey(x => x.BasicConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WearPartDefinition)
            .WithMany(x => x.ExceedLimitRecords)
            .HasForeignKey(x => x.WearPartDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WearPartDefinitionId, x.Severity, x.OccurredAt });
    }
}