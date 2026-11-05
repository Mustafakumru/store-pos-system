using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// İndirim kuponu. Kasiyer ödeme ekranında kupon kodunu girer; indirim
/// tutarı HER ZAMAN backend'de bu entity'nin kurallarıyla hesaplanır
/// (client'ın hesapladığı tutara güvenilmez).
/// </summary>
public class Coupon : BaseEntity
{
    public string Code { get; set; } = null!;          // Unique, büyük harf
    public string? Description { get; set; }

    public DiscountType DiscountType { get; set; }     // Yüzde veya sabit tutar
    public decimal Value { get; set; }                 // %10 için 10, 50 TL için 50

    public decimal? MinPurchaseAmount { get; set; }    // Alt limit (opsiyonel)
    public decimal? MaxDiscountAmount { get; set; }    // Yüzde kuponlarda tavan (opsiyonel)

    public DateTime StartDate { get; set; }            // UTC
    public DateTime? EndDate { get; set; }             // UTC, null = süresiz

    public int? UsageLimit { get; set; }               // null = sınırsız
    public int UsedCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Sale> Sales { get; set; } = [];

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// Kupon bu tutara uygulanamıyorsa sebebini, uygulanabiliyorsa null döner.
    /// </summary>
    public string? GetInvalidReason(DateTime nowUtc, decimal baseAmount)
    {
        if (!IsActive) return "Kupon pasif durumda.";
        if (nowUtc < StartDate) return "Kupon henüz geçerli değil.";
        if (EndDate.HasValue && nowUtc > EndDate.Value) return "Kuponun süresi dolmuş.";
        if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value) return "Kuponun kullanım limiti dolmuş.";
        if (baseAmount <= 0) return "Kuponun uygulanacağı tutar yok.";
        if (MinPurchaseAmount.HasValue && baseAmount < MinPurchaseAmount.Value)
            return $"Kupon en az {MinPurchaseAmount.Value:F2} TL alışverişte geçerlidir.";
        return null;
    }

    /// <summary>Kuponun bu tutara uygulanacak indirimi (tutarı aşamaz).</summary>
    public decimal CalculateDiscount(decimal baseAmount)
    {
        var discount = DiscountType == DiscountType.Percentage
            ? Math.Round(baseAmount * Value / 100m, 2)
            : Value;

        if (MaxDiscountAmount.HasValue)
            discount = Math.Min(discount, MaxDiscountAmount.Value);

        return Math.Min(discount, baseAmount);
    }
}
