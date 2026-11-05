using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ZReportConfiguration : IEntityTypeConfiguration<ZReport>
{
    public void Configure(EntityTypeBuilder<ZReport> builder)
    {
        builder.HasKey(z => z.Id);

        // Mağaza bazında Z no benzersiz olmalı (Mali Rapor aralık sorgusu için)
        builder.HasIndex(z => new { z.StoreId, z.ReportNumber })
            .IsUnique();

        builder.Property(z => z.TotalSalesAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.TotalRefundsAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.NetAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.CashTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.CardTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.GiftCardTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(z => z.CashRefundTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(z => z.CardRefundTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        // Hediye kartı satışı (emanet)
        builder.Property(z => z.GiftCardSoldTotal).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(z => z.GiftCardSoldCash).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(z => z.GiftCardSoldCard).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);

        builder.Property(z => z.CashierId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(z => z.Store)
            .WithMany()
            .HasForeignKey(z => z.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(z => !z.IsDeleted);
    }
}