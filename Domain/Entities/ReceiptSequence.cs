using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Mağaza + gün bazlı fiş numarası sayacı.
/// Her satışta atomik olarak +1 artırılır (race condition önlenir).
/// </summary>
public class ReceiptSequence : BaseEntity
{
    public int StoreId { get; set; }
    public DateTime SequenceDate { get; set; } // Sadece tarih (saat 00:00)
    public int CurrentValue { get; set; }

    // Navigation
    public Store Store { get; set; } = null!;
}