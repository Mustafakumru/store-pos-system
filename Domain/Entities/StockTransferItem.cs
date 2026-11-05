using Domain.Common;

namespace Domain.Entities;

public class StockTransferItem : BaseEntity
{
    public int StockTransferId { get; set; }
    public int VariantId { get; set; }  // ✅ ProductId → VariantId
    public int RequestedQuantity { get; set; }
    public int ShippedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }

    public StockTransfer StockTransfer { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;  // ✅ güncellendi
}