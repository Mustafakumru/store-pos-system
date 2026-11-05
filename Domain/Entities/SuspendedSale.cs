using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Askıya alınan satış sepeti (WPOS "Askıya Al" / "Askıdan Çağır").
/// Sepet içeriği JSON olarak saklanır; mağazadaki herhangi bir kasiyer
/// geri çağırabilir.
/// </summary>
public class SuspendedSale : BaseEntity
{
    public int StoreId { get; set; }
    public string CashierId { get; set; } = string.Empty; // Askıya alan kasiyer
    public string CartDataJson { get; set; } = string.Empty; // Sepet içeriği (items, müşteri, indirim vb.)
    public string? Note { get; set; } // Opsiyonel etiket (örn. "Müşteri kabine gitti")

    // Navigation
    public Store Store { get; set; } = null!;
}