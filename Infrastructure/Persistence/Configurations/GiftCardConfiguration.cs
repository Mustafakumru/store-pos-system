using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class GiftCardConfiguration : IEntityTypeConfiguration<GiftCard>
{
    public void Configure(EntityTypeBuilder<GiftCard> builder)
    {
        // Iyimser eszamanlilik: iki kasa ayni satiri ayni anda degistirirse
        // ikincisi DbUpdateConcurrencyException alir ve komut guncel veriyle
        // yeniden calisir (bkz. ConcurrencyRetryBehavior). Bakiye/stok/iade
        // adedi kayip guncellemeye ugramaz.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasKey(g => g.Id);

        builder.Property(g => g.CardNumber)
            .IsRequired()
            .HasMaxLength(30);

        // Kasiyer kart numarasıyla milisaniyeler içinde sorgulayacak
        builder.HasIndex(g => g.CardNumber)
            .IsUnique();

        builder.Property(g => g.Balance)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}