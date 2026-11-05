using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Ürünün satılabilir varyantı — her barkod bir varyantı temsil eder.
/// POS bu entity'nin barkodunu okur.
/// Örn: Erkek Pantolon → S/Siyah, M/Lacivert, L/Kırmızı
/// </summary>
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }

    // POS'ta okunan barkod — Unique
    public string Barcode { get; set; } = string.Empty;

    // Renk — "Siyah", "Lacivert", "Kırmızı" vb.
    public string? Color { get; set; }

    // Beden — "S","M","L","XL" veya "28","30","32" veya "38","39"
    public string? Size { get; set; }

    // Varyanta özel görsel (opsiyonel — yoksa Product.ImagePath kullanılır)
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Product Product { get; set; } = null!;
    public ICollection<PriceHistory> PriceHistories { get; set; } = [];
    public WarehouseStock? WarehouseStock { get; set; }
    public ICollection<StoreInventory> StoreInventories { get; set; } = [];
    public ICollection<SaleItem> SaleItems { get; set; } = [];

    public int? GroupId { get; set; }
    public ProductVariantGroup? Group { get; set; }
}