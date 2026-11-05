using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Varyant fiyat tarihçesi.
/// EndDate null → aktif fiyat.
/// Fiyat değişince eski kapatılır, yeni açılır — veri ezilmez.
/// </summary>
public class PriceHistory : BaseEntity
{
    // ProductId'den VariantId'ye geçildi
    public int VariantId { get; set; }
    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Navigation
    public ProductVariant Variant { get; set; } = null!;
}