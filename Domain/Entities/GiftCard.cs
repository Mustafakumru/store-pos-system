using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Hediye kartı. WPOS'taki "Hediye Kart Sorgulama" ve "Hediye Kart Yükleme"
/// ekranlarına karşılık gelir. Kart numarası ile sorgulanır.
/// </summary>
public class GiftCard : BaseEntity
{
    public string CardNumber { get; set; } = null!; // Unique
    public decimal Balance { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<GiftCardTransaction> Transactions { get; set; } = [];
}