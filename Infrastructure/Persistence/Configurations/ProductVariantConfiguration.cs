using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Barcode)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(v => v.Barcode)
            .IsUnique();

        builder.Property(v => v.Color)
            .HasMaxLength(50);

        builder.Property(v => v.Size)
            .HasMaxLength(20);

        builder.Property(v => v.ImagePath)
            .HasMaxLength(500);

        builder.HasQueryFilter(v => !v.IsDeleted);

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.PriceHistories)
            .WithOne(ph => ph.Variant)
            .HasForeignKey(ph => ph.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.StoreInventories)
            .WithOne(si => si.Variant)
            .HasForeignKey(si => si.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.SaleItems)
            .WithOne(i => i.Variant)
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}