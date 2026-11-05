using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// İade nedeni lookup tablosu (WPOS "İade Nedeni" ekranı).
/// Örn: Beden, Renk, Delik, Dikiş Hatası, Müşteri Memnuniyeti vb.
/// </summary>
public class ReturnReason : BaseEntity
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<SaleItem> SaleItems { get; set; } = [];
}