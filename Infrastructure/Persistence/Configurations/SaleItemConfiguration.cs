using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        // Iyimser eszamanlilik: iki kasa ayni satiri ayni anda degistirirse
        // ikincisi DbUpdateConcurrencyException alir ve komut guncel veriyle
        // yeniden calisir (bkz. ConcurrencyRetryBehavior). Bakiye/stok/iade
        // adedi kayip guncellemeye ugramaz.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasKey(i => i.Id);

        builder.Property(i => i.BarcodeSnapshot)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.ProductNameSnapshot)
            .IsRequired()
            .HasMaxLength(200);

        // ✅ Yeni snapshot alanları
        builder.Property(i => i.ColorSnapshot)
            .HasMaxLength(50);

        builder.Property(i => i.SizeSnapshot)
            .HasMaxLength(20);

        builder.Property(i => i.PriceAtSale)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.LineTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.StockQuantityAfterSale)
            .IsRequired();
        // ── İndirim alanları ───────────────────────────────────────────
        builder.Property(i => i.DiscountValue)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.DiscountAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);
        // ── İade nedeni ilişkisi ───────────────────────────────────────
        builder.HasOne(i => i.ReturnReason)
            .WithMany(r => r.SaleItems)
            .HasForeignKey(i => i.ReturnReasonId)
            .OnDelete(DeleteBehavior.SetNull);
        // ── İade takibi ────────────────────────────────────────────────
        builder.Property(i => i.RefundedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(i => i.RefundedAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);
        builder.Property(i => i.ApprovedByUserId)
            .HasMaxLength(450); // Identity Id (GUID string) uzunluğu
        // ✅ ProductId → VariantId
        builder.HasOne(i => i.Variant)
            .WithMany(v => v.SaleItems)
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
     
    }
}