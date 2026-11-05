using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Satış kalemi — tüm alanlar snapshot olarak tutulur.
/// Ürün/varyant silinse veya fiyat değişse fiş bozulmaz.
/// </summary>
public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }
    public int VariantId { get; set; }  // ProductId → VariantId

    // ── Snapshot Alanları ────────────────────────────────────
    // Satış anındaki değerler — asla değişmez
    public string BarcodeSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? ColorSnapshot { get; set; }  // ✅ Yeni
    public string? SizeSnapshot { get; set; }  // ✅ Yeni
    public decimal PriceAtSale { get; set; }
    // ─────────────────────────────────────────────────────────
    // ── İndirim Bilgisi (opsiyonel, yönetici onaylı) ──────────────
    public DiscountType? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; } // Girilen tutar veya yüzde
    public decimal DiscountAmount { get; set; } = 0; // Hesaplanan indirim tutarı (TL)
    public string? ApprovedByUserId { get; set; } // Onaylayan yöneticinin Id'si
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public int StockQuantityAfterSale { get; set; }
    // ── İade takibi (Z/X/Mali Rapor için kümülatif) ───────────────
    public int RefundedQuantity { get; set; } = 0;
    public decimal RefundedAmount { get; set; } = 0;
    // Navigation
    public Sale Sale { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    // ── İade nedeni (sadece iade satırlarında set edilir) ─────────
    public int? ReturnReasonId { get; set; }
    public ReturnReason? ReturnReason { get; set; }
}