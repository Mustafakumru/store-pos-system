using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.HasKey(ph => ph.Id);

        builder.Property(ph => ph.Price)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(ph => ph.StartDate)
            .IsRequired();

        // EndDate null → aktif fiyat
        builder.Property(ph => ph.EndDate)
            .IsRequired(false);

        // Soft delete filter
        builder.HasQueryFilter(ph => !ph.IsDeleted);

        // Varyant bazlı hızlı fiyat sorgusu için index
        builder.HasIndex(ph => new { ph.VariantId, ph.EndDate });

        // ✅ ProductId → VariantId
        builder.HasOne(ph => ph.Variant)
            .WithMany(v => v.PriceHistories)
            .HasForeignKey(ph => ph.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}