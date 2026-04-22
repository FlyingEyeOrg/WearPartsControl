using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Configurations;

public sealed class ToolChangeEntityConfiguration : IEntityTypeConfiguration<ToolChangeEntity>
{
    public void Configure(EntityTypeBuilder<ToolChangeEntity> builder)
    {
        builder.ToTable("tool_changes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}