using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Bir satışın ödeme kalemi. Tek satışta birden fazla ödeme tipi
/// olabilir (örn. 100 TL Nakit + 607,66 TL Kart = "Mixed" satış).
/// </summary>
public class SalePayment : BaseEntity
{
    public int SaleId { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal Amount { get; set; }
    public int? InstallmentCount { get; set; } // Kredi kartı taksit sayısı

    // Navigation
    public Sale Sale { get; set; } = null!;
}