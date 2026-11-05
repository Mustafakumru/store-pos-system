using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        // Mağaza kodu unique — "LCW-IST-001" gibi
        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(s => s.Code)
            .IsUnique();

        builder.Property(s => s.City)
            .HasMaxLength(100);

        builder.Property(s => s.Phone)
            .HasMaxLength(20);

        // Global query filter — silinmiş mağazalar sorguya gelmez
        builder.HasQueryFilter(s => !s.IsDeleted);

        // Bir mağazanın birden fazla StoreInventory satırı olabilir
        builder.HasMany(s => s.Inventories)
            .WithOne(i => i.Store)
            .HasForeignKey(i => i.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Sales)
            .WithOne(sale => sale.Store)
            .HasForeignKey(sale => sale.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}