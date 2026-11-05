using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Hediye kartı SATIŞI (bakiye yükleme karşılığı tahsilat).
///
/// Muhasebe: EMANET. Müşteriden alınan para ciro değildir - mağazanın
/// müşteriye borcudur (karttaki bakiye). Ciro, kart alışverişte harcandığında
/// (satışın hediye kartı ödemesi) oluşur. Bu yüzden bu kayıtlar satış/net
/// toplamlarına girmez; raporlarda ayrı "Hediye kartı satışı (emanet)" olarak,
/// kasa sayımında ise alınan nakit olarak görünür.
/// </summary>
public class GiftCardSale : BaseEntity
{
    public int StoreId { get; set; }
    public string CashierId { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;   // "HK001-000042"
    public decimal TotalAmount { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<GiftCardSaleLine> Lines { get; set; } = [];
    public ICollection<GiftCardSalePayment> Payments { get; set; } = [];
}

public class GiftCardSaleLine : BaseEntity
{
    public int GiftCardSaleId { get; set; }
    public int GiftCardId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public GiftCardSale GiftCardSale { get; set; } = null!;
    public GiftCard GiftCard { get; set; } = null!;
}

public class GiftCardSalePayment : BaseEntity
{
    public int GiftCardSaleId { get; set; }
    public PaymentType PaymentType { get; set; }   // sadece Cash / Card
    public decimal Amount { get; set; }             // uygulanan tutar (para üstü düşülmüş)
    public int? InstallmentCount { get; set; }

    public GiftCardSale GiftCardSale { get; set; } = null!;
}
