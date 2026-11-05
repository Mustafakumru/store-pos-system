using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Hediye kartı bakiye hareketi (yükleme, harcama, iade).
/// Audit/log amaçlı — bakiye değişiklikleri burada izlenebilir.
/// </summary>
public class GiftCardTransaction : BaseEntity
{
    public int GiftCardId { get; set; }
    public GiftCardTransactionType Type { get; set; }
    public decimal Amount { get; set; } // Pozitif: yükleme/iade, negatif: harcama
    public decimal BalanceAfter { get; set; } // Hareket sonrası bakiye (audit)
    public int? SaleId { get; set; } // Redeem/Refund ise ilişkili satış

    // Navigation
    public GiftCard GiftCard { get; set; } = null!;
    public Sale? Sale { get; set; }
}