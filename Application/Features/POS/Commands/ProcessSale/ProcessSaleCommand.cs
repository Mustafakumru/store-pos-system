using Domain.Enums;
using MediatR;

namespace Application.Features.POS.Commands.ProcessSale;

public record ProcessSaleCommand(
    List<SaleItemRequest> Items,
    List<PaymentRequest> Payments,
    decimal DiscountAmount = 0,
    int? CustomerId = null,
    string? ApprovalToken = null,
    string? CouponCode = null,      // indirim backend'de hesaplanır (DiscountAmount'a dahil EDİLMEZ)
    int? InstallmentPlan = null     // 4 / 8 = vade farkı (+%5 / +%10) satır tutarlarına eklenir
) : IRequest<ProcessSaleResult>, Application.Common.Behaviors.IConcurrencySensitiveCommand;

public record SaleItemRequest(
    int VariantId,
    int Quantity,
    DiscountType? DiscountType = null,
    decimal? DiscountValue = null
);

/// <summary>
/// Ödeme kalemi. PaymentType olarak sadece Cash/Card/GiftCard
/// gönderilmelidir; "Mixed" satış seviyesinde otomatik hesaplanır.
/// GiftCard ödemesinde CardNumber zorunludur.
/// </summary>
public record PaymentRequest(
    PaymentTypeDto PaymentType,
    decimal Amount,
    int? InstallmentCount = null,
    string? CardNumber = null   // ✅ GiftCard ödemelerinde zorunlu
);

public enum PaymentTypeDto { Cash, Card, Mixed, GiftCard } // ✅ GiftCard eklendi

public record ProcessSaleResult(
    bool Success,
    string ReceiptNumber,
    decimal FinalAmount,
    DateTime SaleDate,
    List<NegativeStockWarning> NegativeStockWarnings
);

public record NegativeStockWarning(
    string ProductName,
    string Barcode,
    string? Color,
    string? Size,
    int StockAfterSale
);