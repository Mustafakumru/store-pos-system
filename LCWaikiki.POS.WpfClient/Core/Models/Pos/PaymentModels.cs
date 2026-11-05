namespace LCWaikiki.POS.WpfClient.Core.Models.Pos;

// ── Odeme Tipleri ────────────────────────────────────────
public enum PaymentType
{
    Cash = 0,
    Card = 1,
    Mixed = 2,
    GiftCard = 3,
    ExchangeCredit = 4   // sadece backend uretir (degisimde iade kredisi) - istemci gondermez
}

// ── Istek Modelleri ──────────────────────────────────────
public record SaleItemRequest(
    int VariantId,
    int Quantity,
    int? DiscountType,
    decimal? DiscountValue
);

public record SalePaymentRequest(
    int PaymentType,
    decimal Amount,
    int? InstallmentCount = null,
    string? CardNumber = null
);

public record ProcessSaleRequest(
    List<SaleItemRequest> Items,
    List<SalePaymentRequest> Payments,
    decimal? DiscountAmount = null,
    int? CustomerId = null,
    string? ApprovalToken = null,
    string? CouponCode = null,      // kupon indirimi backend'de hesaplanir - DiscountAmount'a EKLENMEZ
    int? InstallmentPlan = null     // 4/8: vade farki (+%5/+%10) backend'de satir tutarlarina eklenir
);

// ── Kupon ────────────────────────────────────────────────
public record CouponValidationDto(
    string Code,
    string? Description,
    int DiscountType,   // 0=Amount, 1=Percentage
    decimal Value,
    decimal DiscountAmount
);

// ── Yanit Modelleri ──────────────────────────────────────
public record NegativeStockWarning(
    string ProductName,
    string Barcode,
    string? Color,
    string? Size,
    int StockAfterSale
);

public record ProcessSaleResponse(
    bool Success,
    string ReceiptNumber,
    decimal FinalAmount,
    DateTime SaleDate,
    List<NegativeStockWarning>? NegativeStockWarnings
);

public record GiftCardDto(
    string CardNumber,
    decimal Balance,
    DateTime ExpiryDate,
    bool IsActive
);

public record PriceCheckDto(
    string Barcode,
    string Name,
    decimal CurrentPrice,
    string? Color,
    string? Size,
    int? StockQuantity = null   // magaza stogu (price-check endpoint'i doner)
);