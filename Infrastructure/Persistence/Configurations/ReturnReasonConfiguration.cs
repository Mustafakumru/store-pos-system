using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReturnReasonConfiguration : IEntityTypeConfiguration<ReturnReason>
{
    // Seed kayıtları için sabit tarih (migration deterministik olmalı)
    private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ReturnReason> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.HasQueryFilter(r => !r.IsDeleted);

        // ── WPOS "İade Nedeni" ekranındaki sabit liste ────────────
        builder.HasData(
            new ReturnReason { Id = 1, Name = "Baskı Hatası", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 2, Name = "Beden", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 3, Name = "Delik", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 4, Name = "Diğer", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 5, Name = "Dikiş Açılması", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 6, Name = "Dikiş Bozukluğu", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 7, Name = "Dokuma/Örgü Hatası", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 8, Name = "Düğme", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 9, Name = "Düğme, Kopça, Fermuarlar", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 10, Name = "Esneme", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 11, Name = "Fermuar Hatası", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 12, Name = "Fiyat", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 13, Name = "Kumaş Boyama", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 14, Name = "Model", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 15, Name = "Müşteri Memnuniyeti", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 16, Name = "Renk", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 17, Name = "Renk Solması", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 18, Name = "Tülenme", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 19, Name = "Üründe Aşınma", IsActive = true, CreatedAt = SeedDate },
            new ReturnReason { Id = 20, Name = "Yırtılma", IsActive = true, CreatedAt = SeedDate }
        );
    }
}