using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StoreSalesTargetConfiguration : IEntityTypeConfiguration<StoreSalesTarget>
{
    public void Configure(EntityTypeBuilder<StoreSalesTarget> builder)
    {
        builder.ToTable("StoreSalesTargets");
        builder.Property(t => t.TargetAmount).HasPrecision(18, 2);
        // Mağaza + ay başına tek hedef
        builder.HasIndex(t => new { t.StoreId, t.Year, t.Month }).IsUnique();
        builder.HasOne(t => t.Store).WithMany().HasForeignKey(t => t.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}
