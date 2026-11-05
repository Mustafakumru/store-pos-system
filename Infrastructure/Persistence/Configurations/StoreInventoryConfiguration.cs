using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StoreInventoryConfiguration : IEntityTypeConfiguration<StoreInventory>
{
    public void Configure(EntityTypeBuilder<StoreInventory> builder)
    {
        // Iyimser eszamanlilik: iki kasa ayni satiri ayni anda degistirirse
        // ikincisi DbUpdateConcurrencyException alir ve komut guncel veriyle
        // yeniden calisir (bkz. ConcurrencyRetryBehavior). Bakiye/stok/iade
        // adedi kayip guncellemeye ugramaz.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasKey(si => si.Id);

        // Bir mağazada bir varyantın tek stok satırı
        builder.HasIndex(si => new { si.StoreId, si.VariantId })
            .IsUnique();

        builder.Property(si => si.Quantity)
            .IsRequired()
            .HasDefaultValue(0);
        // Negatife düşebilir — constraint YOK

        builder.Property(si => si.MinStockAlert)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Ignore(si => si.AlertLevel);

        builder.HasQueryFilter(si => !si.IsDeleted);

        builder.HasOne(si => si.Store)
            .WithMany(s => s.Inventories)
            .HasForeignKey(si => si.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.Variant)
            .WithMany(v => v.StoreInventories)
            .HasForeignKey(si => si.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}