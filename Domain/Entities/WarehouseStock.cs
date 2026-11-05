using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Merkezi depodaki varyant stoğu.
/// Her varyantın depoda tek bir stok satırı olur.
/// </summary>
public class WarehouseStock : BaseEntity
{
    public int VariantId { get; set; }  // ProductId → VariantId
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;

    // Navigation
    public ProductVariant Variant { get; set; } = null!;
}