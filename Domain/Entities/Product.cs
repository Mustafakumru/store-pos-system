using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Ana ürün şablonu — katalog kaydı.
/// Barkod ve stok artık ProductVariant'ta tutulur.
/// Örn: "Erkek Slim Fit Pantolon" → birden fazla varyantı olabilir.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // Ürünün genel açıklaması (opsiyonel)
    public string? Description { get; set; }

    // Ana görsel — varyanta özel görsel yoksa bu kullanılır
    public string? ImagePath { get; set; }

    public int CategoryId { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Urun (model) kodu: ornek W6BI21Z8 (bkz. Domain.Common.ProductCode) ──
    // Sunucu uretir, olusturulduktan sonra degismez (barkod gibi kimliktir).
    // Eski kayitlar icin baslangicta geriye donuk doldurulur.
    public string? Code { get; set; }
    public Domain.Enums.Season? Season { get; set; }
    public int? SeasonYear { get; set; }                       // tam yil (2026)
    public Domain.Enums.Department? Department { get; set; }
    public Domain.Enums.AgeGroup? AgeGroup { get; set; }       // Department'tan turetilir (Z1/Z4/Z8)

    // Navigation
    public Category Category { get; set; } = null!;
    public ICollection<ProductVariant> Variants { get; set; } = [];
}