using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Mağazanın aylık net ciro hedefi (Admin belirler). Rapordaki dönem bir aydan
/// kısa ya da birden çok ayı kapsıyorsa hedef gün sayısına göre orantılanır.
/// </summary>
public class StoreSalesTarget : BaseEntity
{
    public int StoreId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TargetAmount { get; set; }

    public Store Store { get; set; } = null!;
}
