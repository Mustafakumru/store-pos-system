using System.Text.RegularExpressions;
using Application.Features.POS.Commands.ProcessSale;
using Domain.Entities;

namespace Application.Features.POS.Common;

/// <summary>
/// Çevrimdışı satış kuralları. Kasa sunucuya ulaşamadığında satışı diske yazar,
/// bağlantı gelince aynı Idempotency-Key ile gönderir. Sunucu:
///  • satışın yapıldığı anı (SoldAt) makul aralıkta kabul eder (en fazla 7 gün önce),
///  • o andaki fiyatı esas alır; kasanın uyguladığı fiyat farklıysa bu fiyatın
///    son <see cref="PriceGraceHours"/> saatte gerçekten geçerli olmuş olmasını ister
///    (kasanın yerel kataloğu eski olabilir ama uydurma fiyat kabul edilmez),
///  • sunucu onayı gerektiren hiçbir şeye (kupon, hediye kartı, indirim onayı) izin vermez.
/// </summary>
public static class OfflineSales
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);
    public const int PriceGraceHours = 48;

    private static readonly Regex ReferencePattern = new("^[0-9A-Z]{8,30}$", RegexOptions.Compiled);

    public static DateTime Validate(ProcessSaleCommand request, DateTime nowUtc)
    {
        var info = request.Offline!;
        if (string.IsNullOrWhiteSpace(info.Reference) || !ReferencePattern.IsMatch(info.Reference))
            throw new InvalidOperationException("Çevrimdışı belge numarası geçersiz.");

        var soldAt = info.SoldAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(info.SoldAt, DateTimeKind.Utc)
            : info.SoldAt.ToUniversalTime();
        if (soldAt > nowUtc + ClockSkew)
            throw new InvalidOperationException("Çevrimdışı satışın tarihi ileri bir zaman olamaz (kasa saatini kontrol edin).");
        if (nowUtc - soldAt > MaxAge)
            throw new InvalidOperationException(
                $"Çevrimdışı satış {MaxAge.TotalDays:0} günden eski; otomatik kaydedilemez. Mağaza müdürüyle elle kayıt yapın.");

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
            throw new InvalidOperationException("Çevrimdışı satışta kupon kullanılamaz.");
        if (request.Payments.Any(p => p.PaymentType == PaymentTypeDto.GiftCard))
            throw new InvalidOperationException("Çevrimdışı satışta hediye kartıyla ödeme alınamaz.");
        if (request.DiscountAmount > 0 || request.Items.Any(i => i.DiscountValue is > 0)
            || !string.IsNullOrWhiteSpace(request.ApprovalToken))
            throw new InvalidOperationException("Çevrimdışı satışta indirim uygulanamaz (yönetici onayı sunucu gerektirir).");

        return soldAt;
    }

    /// <summary>Verilen andaki fiyat (yoksa 0).</summary>
    public static decimal PriceAt(IEnumerable<PriceHistory> history, DateTime at)
        => history
            .Where(ph => ph.StartDate <= at && (ph.EndDate == null || ph.EndDate > at))
            .OrderByDescending(ph => ph.StartDate)
            .Select(ph => ph.Price)
            .FirstOrDefault();

    /// <summary>
    /// Çevrimdışı satışın birim fiyatı. Kasanın uyguladığı fiyat satış anındaki fiyatla
    /// aynıysa ya da satıştan önceki <see cref="PriceGraceHours"/> saat içinde geçerli
    /// olmuş bir fiyatsa (eski katalog) o fiyat kullanılır; aksi halde reddedilir.
    /// </summary>
    public static decimal ResolvePrice(ICollection<PriceHistory> history, DateTime soldAt, decimal? appliedPrice, string productName)
    {
        var priceAtSale = PriceAt(history, soldAt);
        if (appliedPrice is null) return priceAtSale;
        if (appliedPrice == priceAtSale) return priceAtSale;

        var windowStart = soldAt.AddHours(-PriceGraceHours);
        var wasValidRecently = history.Any(ph =>
            ph.Price == appliedPrice
            && ph.StartDate <= soldAt
            && (ph.EndDate == null || ph.EndDate > windowStart));
        if (wasValidRecently) return appliedPrice.Value;

        throw new InvalidOperationException(
            $"'{productName}' çevrimdışı satışta {appliedPrice:N2} TL'ye satılmış; bu fiyat satış tarihinde " +
            $"geçerli değildi (geçerli fiyat {priceAtSale:N2} TL). Mağaza müdürü kontrol etmeli.");
    }
}
