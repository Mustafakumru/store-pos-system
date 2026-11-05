using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransferNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(t => t.TransferNumber)
            .IsUnique();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasDefaultValue(TransferStatus.Draft)
            .HasConversion<string>();

        builder.Property(t => t.Notes)
            .HasMaxLength(500);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne(t => t.ToStore)
            .WithMany()
            .HasForeignKey(t => t.ToStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Items)
            .WithOne(i => i.StockTransfer)
            .HasForeignKey(i => i.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockTransferItemConfiguration
    : IEntityTypeConfiguration<StockTransferItem>
{
    public void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.RequestedQuantity).IsRequired();
        builder.Property(i => i.ShippedQuantity)
            .IsRequired()
            .HasDefaultValue(0);
        builder.Property(i => i.ReceivedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        // ✅ ProductId → VariantId
        builder.HasOne(i => i.Variant)
            .WithMany()
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}