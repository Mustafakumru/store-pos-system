using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class WarehouseStockConfiguration : IEntityTypeConfiguration<WarehouseStock>
{
    public void Configure(EntityTypeBuilder<WarehouseStock> builder)
    {
        builder.HasKey(w => w.Id);

        // Her varyantın depoda tek stok satırı
        builder.HasIndex(w => w.VariantId)
            .IsUnique();

        builder.Property(w => w.Quantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.ReservedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Ignore(w => w.AvailableQuantity);

        builder.HasQueryFilter(w => !w.IsDeleted);

        builder.HasOne(w => w.Variant)
            .WithOne()
            .HasForeignKey<WarehouseStock>(w => w.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}