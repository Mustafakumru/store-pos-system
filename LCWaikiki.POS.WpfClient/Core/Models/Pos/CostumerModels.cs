namespace LCWaikiki.POS.WpfClient.Core.Models.Pos;

/// <summary>
/// Backend'in GET /pos/customers/search ve POST /pos/customers
/// endpoint'lerinin dondugu DTO'nun mirror'i.
/// </summary>
public record CustomerDto(
    int Id,
    string PhoneNumber,
    string FullName,
    string? Email,
    bool SmsConsent
);

/// <summary>
/// GET /pos/customers/{id}/purchases?barcode=... endpoint'inin dondugu DTO'nun
/// mirror'i. Musterinin belirli bir urunu gecmiste alip almadigini gosterir
/// (degisim akisinda kullanilir).
/// </summary>
public record CustomerPurchaseDto(
    string ReceiptNumber,
    DateTime SaleDate,
    int StoreId,
    string StoreName,
    int VariantId,
    string Barcode,
    string ProductName,
    string? Color,
    string? Size,
    int Quantity,
    decimal PriceAtSale,
    decimal LineTotal,
    int RefundedQuantity,
    decimal RefundableUnitPrice = 0   // backend'in hesapladigi birim iade tutari
)
{
    /// <summary>Iade birim fiyati: backend degeri; eski sunucuda LineTotal/Adet.</summary>
    public decimal EffectiveRefundUnitPrice => RefundableUnitPrice > 0
        ? RefundableUnitPrice
        : (Quantity > 0 ? LineTotal / Quantity : PriceAtSale);
}

/// <summary>
/// POST /pos/customers icin request govdesi.
/// Backend'deki CreateCustomerCommand(PhoneNumber, FullName, Email?, SmsConsent)
/// ile bire bir eslesir.
/// </summary>
public record CreateCustomerRequest(
    string PhoneNumber,
    string FullName,
    string? Email,
    bool SmsConsent
);