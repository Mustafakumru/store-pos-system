using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CashDrawerSessionConfiguration : IEntityTypeConfiguration<CashDrawerSession>
{
    public void Configure(EntityTypeBuilder<CashDrawerSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OpeningFloat)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.ClosingAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.CashierId)
            .IsRequired()
            .HasMaxLength(450);

        // Bir kasiyerin açık oturumunu hızlı bulmak için
        builder.HasIndex(s => new { s.StoreId, s.CashierId, s.Status });

        // Bir kasiyerin bir mağazada AYNI ANDA tek açık oturumu olabilir.
        // Uygulama kontrolü (AnyAsync) eşzamanlı isteklerde yetersizdi:
        // aynı anda gelen istekler birden fazla açık oturum oluşturabiliyordu.
        builder.HasIndex(s => new { s.StoreId, s.CashierId })
            .IsUnique()
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0")
            .HasDatabaseName("IX_CashDrawerSessions_OneOpenPerCashier");

        builder.HasOne(s => s.Store)
            .WithMany()
            .HasForeignKey(s => s.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}