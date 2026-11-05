using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CashierId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(r => r.Note).HasMaxLength(500);

        // Raporlar mağaza + tarih aralığıyla sorgular
        builder.HasIndex(r => new { r.StoreId, r.CreatedAt });
        builder.HasIndex(r => new { r.CashierId, r.CreatedAt });

        builder.HasOne(r => r.Store)
            .WithMany()
            .HasForeignKey(r => r.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ExchangeSale)
            .WithMany()
            .HasForeignKey(r => r.ExchangeSaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines)
            .WithOne(l => l.SaleReturn)
            .HasForeignKey(l => l.SaleReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Payouts)
            .WithOne(p => p.SaleReturn)
            .HasForeignKey(p => p.SaleReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.BarcodeSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(l => l.ProductNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(l => l.OriginalReceiptNumber).HasMaxLength(50);
        builder.Property(l => l.Amount).IsRequired().HasColumnType("decimal(18,2)");

        builder.HasIndex(l => l.SaleItemId);
        builder.HasIndex(l => l.OriginalSaleId);

        builder.HasOne(l => l.SaleItem)
            .WithMany()
            .HasForeignKey(l => l.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ReturnReason)
            .WithMany()
            .HasForeignKey(l => l.ReturnReasonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}

public class SaleReturnPayoutConfiguration : IEntityTypeConfiguration<SaleReturnPayout>
{
    public void Configure(EntityTypeBuilder<SaleReturnPayout> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(p => p.GiftCardNumber).HasMaxLength(50);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
