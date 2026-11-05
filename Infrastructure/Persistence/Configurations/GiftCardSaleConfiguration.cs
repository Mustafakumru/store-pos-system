using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class GiftCardSaleConfiguration : IEntityTypeConfiguration<GiftCardSale>
{
    public void Configure(EntityTypeBuilder<GiftCardSale> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.CashierId).IsRequired().HasMaxLength(450);
        builder.Property(s => s.DocumentNumber).IsRequired().HasMaxLength(30);
        builder.Property(s => s.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");

        builder.HasIndex(s => new { s.StoreId, s.CreatedAt });
        builder.HasIndex(s => new { s.CashierId, s.CreatedAt });

        builder.HasOne(s => s.Store).WithMany().HasForeignKey(s => s.StoreId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines).WithOne(l => l.GiftCardSale)
            .HasForeignKey(l => l.GiftCardSaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Payments).WithOne(p => p.GiftCardSale)
            .HasForeignKey(p => p.GiftCardSaleId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class GiftCardSaleLineConfiguration : IEntityTypeConfiguration<GiftCardSaleLine>
{
    public void Configure(EntityTypeBuilder<GiftCardSaleLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.CardNumber).IsRequired().HasMaxLength(50);
        builder.Property(l => l.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.HasOne(l => l.GiftCard).WithMany().HasForeignKey(l => l.GiftCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}

public class GiftCardSalePaymentConfiguration : IEntityTypeConfiguration<GiftCardSalePayment>
{
    public void Configure(EntityTypeBuilder<GiftCardSalePayment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
