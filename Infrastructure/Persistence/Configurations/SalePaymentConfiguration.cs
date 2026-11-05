using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.HasOne(p => p.Sale)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}