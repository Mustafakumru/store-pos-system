namespace LCWaikiki.POS.WpfClient.Core.Models.Pos;

/// <summary>
/// GET /pos/sales/{receiptNumber} endpoint'inin dondugu DTO'nun mirror'i.
/// "Fisle Ara" akisinda kullanilir.
/// </summary>
public record SaleReceiptDto(
	int Id,
	string ReceiptNumber,
	int StoreId,
	string StoreName,
	string CashierId,
	DateTime SaleDate,
	string PaymentType,
	string Status,
	decimal TotalAmount,
	decimal DiscountAmount,
	decimal FinalAmount,
	bool HasNegativeStockItem,
	List<SaleItemDto> Items,
	List<SalePaymentDto> Payments,
	int? CustomerId = null,          // satisin musterisi (fisle degisimde tasinir)
	string? CustomerName = null,
	string? CustomerPhone = null
);

public record SaleItemDto(
	int VariantId,
	string Barcode,
	string ProductName,
	string? Color,
	string? Size,
	decimal PriceAtSale,
	int Quantity,
	decimal LineTotal,
	int StockAfterSale,
	string? DiscountType,
	decimal DiscountAmount,
	int RefundedQuantity = 0,          // daha once iade edilen adet
	decimal RefundableUnitPrice = 0    // backend'in hesapladigi birim iade tutari (belge indirimi dusulmus)
)
{
	/// <summary>Iade edilebilecek kalan adet.</summary>
	public int RemainingQuantity => Quantity - RefundedQuantity;

	/// <summary>Iade birim fiyati: backend degeri; eski sunucuda LineTotal/Adet.</summary>
	public decimal EffectiveRefundUnitPrice => RefundableUnitPrice > 0
		? RefundableUnitPrice
		: (Quantity > 0 ? LineTotal / Quantity : PriceAtSale);
}

public record SalePaymentDto(
	string PaymentType,
	decimal Amount,
	int? InstallmentCount
);

/// <summary>
/// Backend'in ProcessRefundCommand'inin dondugu sonucun mirror'i.
/// ProcessRefundCommand parayi hicbir yere fiilen odemez - sadece stok +
/// SaleItem.RefundedQuantity/RefundedAmount gunceller, RefundAmount'i
/// BILGI olarak doner. Musteriye fiili odeme (nakit/kart/hediye karti)
/// tamamen client tarafinda ayri bir adimda yapilir.
/// </summary>
public record ProcessRefundResult(
	bool Success,
	decimal RefundAmount,
	int RefundedItemCount,
	string Message,
	int? SaleReturnId = null
);

/// <summary>
/// POST /pos/giftcards/{cardNumber}/load icin request govdesi.
/// </summary>
public record LoadGiftCardRequest(
	decimal Amount,
	int? SaleId
);

/// <summary>
/// POST /pos/sales/{receiptNumber}/refund icin bir iade kalemi.
/// </summary>
public record RefundItemRequest(
	int VariantId,
	int RefundQuantity,
	int? ReturnReasonId
);

/// <summary>
/// POST /pos/sales/{receiptNumber}/refund icin request govdesi
/// (backend'deki RefundBody ile eslesir).
/// </summary>
public record RefundRequest(
	List<RefundItemRequest> Items,
	string? Reason,
	int? PayoutMethod = null,      // RefundPayoutMethod: 0=Nakit (varsayilan), 1=Kart, 2=Hediye karti
	string? GiftCardNumber = null
);

/// <summary>Iade tutarinin musteriye odenme yontemi (backend RefundMethodDto ile ayni sira).</summary>
public enum RefundPayoutMethod
{
	Cash = 0,
	Card = 1,
	GiftCard = 2
}

// ── Iade Nedenleri ─────────────────────────────────────────────────

/// <summary>
/// GET /pos/return-reasons endpoint'inin dondugu DTO'nun mirror'i.
/// </summary>
public record ReturnReasonDto(
	int Id,
	string Name
);

// ── Degisim ──────────────────────────────────────────────────────

/// <summary>
/// POST /pos/exchange icin bir iade kalemi. RefundItemRequest'ten farki:
/// ReceiptNumber'i kendi icinde tasir (nullable - kayitsiz/izsiz iadede
/// null gonderilir) ve kayitsiz iadede kasiyerin girdigi ManualUnitPrice'i
/// destekler.
/// </summary>
public record ExchangeReturnItemRequest(
	string? ReceiptNumber,
	int VariantId,
	int RefundQuantity,
	int? ReturnReasonId,
	decimal? ManualUnitPrice = null
);

/// <summary>
/// POST /pos/exchange icin request govdesi (backend'deki
/// ProcessExchangeCommand ile eslesir).
/// </summary>
public record ProcessExchangeRequest(
	List<ExchangeReturnItemRequest> ReturnItems,
	List<SaleItemRequest> NewItems,
	List<SalePaymentRequest> SettlementPayments,
	int? CustomerId,
	decimal DiscountAmount = 0,
	string? ApprovalToken = null,
	int? InstallmentPlan = null,
	int? OwedRefundMethod = null,      // musteriye kalan fark: RefundPayoutMethod
	string? OwedGiftCardNumber = null,
	string? ReturnApprovalToken = null // fissiz iade varsa yonetici onayi
);

/// <summary>
/// POST /pos/exchange sonucunun mirror'i.
/// </summary>
public record ProcessExchangeResult(
	bool Success,
	string? NewReceiptNumber,
	decimal ReturnTotal,
	decimal NewTotal,
	decimal NetAmount,
	decimal AmountCollectedFromCustomer,
	decimal AmountOwedToCustomer,
	string Message,
	decimal ExchangeCreditApplied = 0,   // yeni satista kullanilan iade kredisi
	decimal NewSaleFinalAmount = 0,      // yeni satisin tam tutari (fis toplami)
	string? OwedRefundMethod = null
);