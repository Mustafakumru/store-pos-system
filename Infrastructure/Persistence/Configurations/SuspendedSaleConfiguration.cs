using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SuspendedSaleConfiguration : IEntityTypeConfiguration<SuspendedSale>
{
    public void Configure(EntityTypeBuilder<SuspendedSale> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CashierId)
            .IsRequired()
            .HasMaxLength(450); // Identity Id (GUID string) uzunluğu

        builder.Property(s => s.CartDataJson)
            .IsRequired();

        builder.Property(s => s.Note)
            .HasMaxLength(200);

        // Mağazadaki askıdaki sepetleri listelemek için
        builder.HasIndex(s => s.StoreId);

        builder.HasOne(s => s.Store)
            .WithMany()
            .HasForeignKey(s => s.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}