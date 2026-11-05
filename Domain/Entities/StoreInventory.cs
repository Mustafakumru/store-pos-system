using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Mağazadaki varyant stoğu.
/// Negatife düşebilir — bilinçli karar.
/// </summary>
public class StoreInventory : BaseEntity
{
    public int StoreId { get; set; }
    public int VariantId { get; set; }  // ProductId → VariantId
    public int Quantity { get; set; }
    public int MinStockAlert { get; set; } = 5;

    public StockAlertLevel AlertLevel => Quantity switch
    {
        < 0 => StockAlertLevel.Critical,
        0 => StockAlertLevel.OutOfStock,
        var q when q <= MinStockAlert => StockAlertLevel.Low,
        _ => StockAlertLevel.Normal
    };

    // Navigation
    public Store Store { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}