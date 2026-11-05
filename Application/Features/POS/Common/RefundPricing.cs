using Domain.Entities;

namespace Application.Features.POS.Common;

/// <summary>
/// İade tutarı = müşterinin o kalem için GERÇEKTE ödediği tutar.
///
/// SaleItem.LineTotal satır indirimi ve vade farkını içerir ama belge
/// seviyesindeki indirimi (ara toplam indirimi + kupon) içermez. Eskiden iade
/// LineTotal/Adet üzerinden yapılıyordu: %20 ara toplam indirimiyle 80 TL'ye
/// satılan ürün için 100 TL iade ediliyordu. Burada belge indirimi satırlara
/// tutarları oranında dağıtılır.
/// </summary>
public static class RefundPricing
{
    /// <summary>Satışta belge indirimi sonrası ödenen pay (0..1).</summary>
    public static decimal PaidFactor(Sale sale)
    {
        if (sale.TotalAmount <= 0) return 1m;
        var paid = sale.TotalAmount - sale.DiscountAmount;
        return Math.Clamp(paid / sale.TotalAmount, 0m, 1m);
    }

    /// <summary>Kalemin iade edilebilir toplam tutarı (tüm adetler).</summary>
    public static decimal LineRefundable(Sale sale, SaleItem item)
        => Math.Round(item.LineTotal * PaidFactor(sale), 2);

    /// <summary>Ekranda gösterilecek birim iade fiyatı.</summary>
    public static decimal UnitRefundable(Sale sale, SaleItem item)
        => item.Quantity > 0 ? Math.Round(LineRefundable(sale, item) / item.Quantity, 2) : 0m;

    /// <summary>
    /// Bu iadede <paramref name="quantity"/> adet için ödenecek tutar.
    /// Kalan adetlerin tamamı iade ediliyorsa kuruş farkı kalmasın diye
    /// "iade edilebilir toplam − daha önce iade edilen" kullanılır.
    /// </summary>
    public static decimal AmountFor(Sale sale, SaleItem item, int quantity)
    {
        var remainingQty = item.Quantity - item.RefundedQuantity;
        var lineRefundable = LineRefundable(sale, item);

        if (quantity >= remainingQty)
            return Math.Max(0m, lineRefundable - item.RefundedAmount);

        return item.Quantity > 0
            ? Math.Round(lineRefundable / item.Quantity * quantity, 2)
            : 0m;
    }
}
