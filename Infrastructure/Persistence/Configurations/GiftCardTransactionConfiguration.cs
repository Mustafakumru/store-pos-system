using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class GiftCardTransactionConfiguration
    : IEntityTypeConfiguration<GiftCardTransaction>
{
    public void Configure(EntityTypeBuilder<GiftCardTransaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.BalanceAfter)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.HasOne(t => t.GiftCard)
            .WithMany(g => g.Transactions)
            .HasForeignKey(t => t.GiftCardId)
            .OnDelete(DeleteBehavior.Restrict);

        // Redeem/Refund işlemleri bir satışla ilişkilendirilebilir (opsiyonel)
        builder.HasOne(t => t.Sale)
            .WithMany()
            .HasForeignKey(t => t.SaleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}