using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(2);
        // Kategori kodu urun kodunun parcasi: benzersiz (bos olanlar haric)
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
    }
}
