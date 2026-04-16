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
        builder.Property(x => x.Remark).HasMaxLength(512);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.ResourceNumber).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PartName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CurrentValueAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.WarningValueAddress).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ShutdownValueAddress).HasMaxLength(128).IsRequired();

        builder.HasOne(x => x.BasicConfiguration)
            .WithMany(x => x.WearPartDefinitions)
            .HasForeignKey(x => x.BasicConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BasicConfigurationId, x.PartName }).IsUnique();
    }
}
