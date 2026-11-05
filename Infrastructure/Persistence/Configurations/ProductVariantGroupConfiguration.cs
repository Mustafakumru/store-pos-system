using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductVariantGroupConfiguration
    : IEntityTypeConfiguration<ProductVariantGroup>
{
    public void Configure(EntityTypeBuilder<ProductVariantGroup> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.GroupType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Color");

        builder.HasQueryFilter(g => !g.IsDeleted);

        builder.HasOne(g => g.Product)
            .WithMany()
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Variants)
            .WithOne(v => v.Group)
            .HasForeignKey(v => v.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(g => g.Images)
            .WithOne(i => i.Group)
            .HasForeignKey(i => i.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductVariantGroupImageConfiguration
    : IEntityTypeConfiguration<ProductVariantGroupImage>
{
    public void Configure(EntityTypeBuilder<ProductVariantGroupImage> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ImagePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.DisplayOrder)
            .HasDefaultValue(0);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}