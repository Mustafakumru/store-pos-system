using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(s => s.Id);

        // İyimser eşzamanlılık: aynı fişe eşzamanlı iadeler (bkz. ReturnProcessor)
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Property(s => s.RefundedAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(s => s.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(50);

        // Fiş numarası ile hızlı arama (Son Belge Tekrarı, İade vb.)
        builder.HasIndex(s => s.ReceiptNumber)
            .IsUnique();

        builder.Property(s => s.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.DiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.FinalAmount)
            .HasColumnType("decimal(18,2)");

        // ── Müşteri ilişkisi ───────────────────────────────────
        builder.HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.OfflineReference).HasMaxLength(30);
        // Aynı çevrimdışı belge iki kez kaydedilemez (Idempotency-Key'e ek ikinci savunma)
        builder.HasIndex(s => new { s.StoreId, s.OfflineReference })
            .IsUnique()
            .HasFilter("[OfflineReference] IS NOT NULL");

        builder.HasIndex(s => s.CustomerId);

        // ── Kupon ilişkisi ─────────────────────────────────────
        builder.Property(s => s.CouponDiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.HasOne(s => s.Coupon)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.CouponId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}