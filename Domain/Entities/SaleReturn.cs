using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// İade defteri kaydı - her iade/değişim işleminin iade tarafı.
///
/// Raporlar iadeyi BURADAN okur:
///  • StoreId = iadeyi YAPAN mağaza (mağazalar arası iadede orijinal satışın
///    mağazası değil - stok ve para bu mağazada hareket eder)
///  • CreatedAt = iadenin gerçek anı (SaleItem.UpdatedAt değil)
///  • Payouts = tutarın müşteriye nasıl ödendiği (nakit / kart / hediye kartı /
///    değişim kredisi) - kasa sayımı nakit iadeleri buradan düşer.
///
/// Fişsiz (kayıtsız) iadeler de buraya yazılır; satırlarında SaleItemId boştur.
/// </summary>
public class SaleReturn : BaseEntity
{
    public int StoreId { get; set; }
    public string CashierId { get; set; } = string.Empty;

    /// <summary>Değişimde oluşan yeni satış (iade kredisi bu satışta kullanıldı).</summary>
    public int? ExchangeSaleId { get; set; }

    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public Store Store { get; set; } = null!;
    public Sale? ExchangeSale { get; set; }
    public ICollection<SaleReturnLine> Lines { get; set; } = [];
    public ICollection<SaleReturnPayout> Payouts { get; set; } = [];
}

public class SaleReturnLine : BaseEntity
{
    public int SaleReturnId { get; set; }

    /// <summary>İade edilen satış kalemi. Fişsiz iadede null.</summary>
    public int? SaleItemId { get; set; }
    public int? OriginalSaleId { get; set; }
    public string? OriginalReceiptNumber { get; set; }

    public int VariantId { get; set; }
    public string BarcodeSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public int? ReturnReasonId { get; set; }

    public SaleReturn SaleReturn { get; set; } = null!;
    public SaleItem? SaleItem { get; set; }
    public ReturnReason? ReturnReason { get; set; }
}

public class SaleReturnPayout : BaseEntity
{
    public int SaleReturnId { get; set; }
    public RefundMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? GiftCardNumber { get; set; }

    public SaleReturn SaleReturn { get; set; } = null!;
}
