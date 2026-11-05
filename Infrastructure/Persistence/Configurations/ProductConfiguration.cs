using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.ImagePath)
            .HasMaxLength(500);

        // ── Urun kodu (W6BI21Z8): 8 karakter, benzersiz ──
        builder.Property(p => p.Code)
            .HasMaxLength(8)
            .IsUnicode(false);
        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasFilter("[Code] IS NOT NULL");
        builder.Property(p => p.Season).HasConversion<int>();
        builder.Property(p => p.Department).HasConversion<int>();
        builder.Property(p => p.AgeGroup).HasConversion<int>();

        // ✅ Barcode index kaldırıldı — barkod artık ProductVariant'ta
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Category.Products ile AYNI iliski (eskiden WithMany() bos birakildigi
        // icin EF ikinci bir golge iliski + bos CategoryId1 kolonu aciyordu)
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}