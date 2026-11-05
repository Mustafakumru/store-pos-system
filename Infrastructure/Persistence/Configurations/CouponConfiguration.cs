using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        // Iyimser eszamanlilik: iki kasa ayni satiri ayni anda degistirirse
        // ikincisi DbUpdateConcurrencyException alir ve komut guncel veriyle
        // yeniden calisir (bkz. ConcurrencyRetryBehavior). Bakiye/stok/iade
        // adedi kayip guncellemeye ugramaz.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(200);

        builder.Property(c => c.Value)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.MinPurchaseAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.MaxDiscountAmount)
            .HasColumnType("decimal(18,2)");

        // Aynı anda iki kasada son kullanım hakkı harcanırsa biri
        // DbUpdateConcurrencyException alır - limit aşılmaz.
        builder.Property(c => c.UsedCount)
            .IsConcurrencyToken();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
