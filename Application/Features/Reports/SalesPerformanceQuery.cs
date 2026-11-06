using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports;

/// <summary>
/// Admin satış performansı: seçilen dönemde (yerel iş günleri, iki uç dahil) tüm mağazaların
/// net cirosu, hedefe göre gerçekleşmesi, sıralaması, günlük seyir ve en çok satanlar.
///
/// Net ciro mağaza panelindeki ve X/Z raporundaki tanımla AYNIDIR (SalesReportCalculator):
///   dönemde kesilen fişlerin FinalAmount toplamı − dönemde yapılan iadeler (iade defteri),
///   ikisi de işlemin YAPILDIĞI mağazaya yazılır. Hediye kartı satışı emanettir, ciroya girmez.
///
/// Hedef: aylık (StoreSalesTarget). Dönem ayın tamamı değilse gün sayısına göre orantılanır;
/// dönemin herhangi bir ayında hedef yoksa o mağazanın dönem hedefi yoktur (yanıltıcı oran
/// göstermemek için). "Olması gereken" ilerleme: hedef × geçen gün / dönemdeki gün.
/// </summary>
public record GetSalesPerformanceQuery(DateTime FromLocalDate, DateTime ToLocalDate) : IRequest<SalesPerformanceDto>;

/// <summary>Tarihler YEREL gün (DateOnly, JSON'da "yyyy-MM-dd"): saat/UTC kayması olmasın.</summary>
public record SalesPerformanceDto(
    DateOnly From, DateOnly To, int Days, int ElapsedDays,
    SalesPerformanceTotals Totals,
    List<StorePerformanceDto> Stores,
    List<DailyRevenueDto> Daily,
    List<TopProductDto> TopProducts);

public record SalesPerformanceTotals(
    decimal NetRevenue, decimal SalesAmount, decimal RefundAmount, int SaleCount, int RefundCount,
    int ItemsSold, decimal AverageBasket, decimal? TargetAmount, decimal? AchievementPercent,
    decimal PreviousNetRevenue, decimal? ChangePercent);

public record StorePerformanceDto(
    int Rank, int StoreId, string StoreName, string StoreCode, bool IsActive,
    decimal NetRevenue, decimal SalesAmount, decimal RefundAmount, int SaleCount, int ItemsSold,
    decimal AverageBasket, decimal? TargetAmount, decimal? AchievementPercent,
    decimal? ExpectedPercent, string Pace, decimal? RemainingToTarget,
    decimal PreviousNetRevenue, decimal? ChangePercent, decimal ShareOfTotal);

public record DailyRevenueDto(DateOnly Date, decimal NetRevenue, int SaleCount);

public record TopProductDto(int VariantId, string ProductName, string? Color, string? Size, int Quantity, decimal Revenue);

public class GetSalesPerformanceQueryHandler : IRequestHandler<GetSalesPerformanceQuery, SalesPerformanceDto>
{
    public const int MaxDays = 366;
    private readonly IApplicationDbContext _db;
    public GetSalesPerformanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SalesPerformanceDto> Handle(GetSalesPerformanceQuery q, CancellationToken ct)
    {
        var from = q.FromLocalDate.Date;
        var to = q.ToLocalDate.Date;
        if (to < from) throw new InvalidOperationException("Bitiş tarihi başlangıçtan önce olamaz.");
        var days = (to - from).Days + 1;
        if (days > MaxDays) throw new InvalidOperationException($"Rapor en fazla {MaxDays} günlük olabilir.");

        var today = BusinessClock.Today;
        var elapsed = today < from ? 0 : today > to ? days : (today - from).Days + 1;

        var (fromUtc, _) = BusinessClock.DayRangeUtc(from);
        var (_, toUtc) = BusinessClock.DayRangeUtc(to);
        // Önceki dönem: AYNI UZUNLUKTA, dönemin bugüne kadar geçen kısmı kadar. "Bu ay" (6. gün)
        // önceki 30 günle değil, önceki dönemin ilk 6 günüyle karşılaştırılır (yoksa −%77 gibi
        // yanıltıcı düşüş görünüyordu).
        var comparedDays = Math.Max(1, elapsed == 0 ? days : elapsed);
        var (prevFromUtc, _) = BusinessClock.DayRangeUtc(from.AddDays(-days));
        var (_, prevToUtc) = BusinessClock.DayRangeUtc(from.AddDays(-days + comparedDays - 1));
        var (_, currentComparableToUtc) = BusinessClock.DayRangeUtc(from.AddDays(comparedDays - 1));

        // ── Mağaza bazında satış / iade (dönem + önceki eşit dönem) ──────────
        var current = await AggregateAsync(fromUtc, toUtc, ct);
        var previous = await AggregateAsync(prevFromUtc, prevToUtc, ct);
        var currentComparable = elapsed > 0 && elapsed < days ? await AggregateAsync(fromUtc, currentComparableToUtc, ct) : current;

        var stores = await _db.Stores.AsNoTracking().IgnoreQueryFilters()
            .Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.Name, s.Code, s.IsActive })
            .ToListAsync(ct);

        // ── Hedefler: dönemin kapsadığı aylar ─────────────────────────────
        var months = Enumerable.Range(0, days).Select(i => from.AddDays(i))
            .GroupBy(d => (d.Year, d.Month))
            .Select(g => (g.Key.Year, g.Key.Month, Days: g.Count()))
            .ToList();
        var monthKeys = months.Select(m => m.Year * 100 + m.Month).ToList();
        var targets = await _db.StoreSalesTargets.AsNoTracking()
            .Where(t => monthKeys.Contains(t.Year * 100 + t.Month))
            .ToListAsync(ct);

        decimal? PeriodTarget(int storeId)
        {
            decimal sum = 0;
            foreach (var (year, month, inPeriod) in months)
            {
                var t = targets.FirstOrDefault(x => x.StoreId == storeId && x.Year == year && x.Month == month);
                if (t is null) return null;
                sum += t.TargetAmount * inPeriod / DateTime.DaysInMonth(year, month);
            }
            return Math.Round(sum, 2);
        }

        var totalNet = current.Values.Sum(c => c.Net);
        var rows = stores
            .Where(s => s.IsActive || current.ContainsKey(s.Id) || targets.Any(t => t.StoreId == s.Id))
            .Select(s =>
            {
                var c = current.GetValueOrDefault(s.Id) ?? StoreAgg.Empty;
                var p = previous.GetValueOrDefault(s.Id) ?? StoreAgg.Empty;
                var target = PeriodTarget(s.Id);
                decimal? achievement = target is > 0 ? Math.Round(c.Net / target.Value * 100, 1) : null;
                decimal? expected = target is > 0 ? Math.Round((decimal)elapsed / days * 100, 1) : null;
                return new
                {
                    s, c, p, target, achievement, expected,
                    Pace = Pace(achievement, expected),
                    Remaining = target is > 0 ? Math.Max(0, target.Value - c.Net) : (decimal?)null
                };
            })
            // Sıralama: hedefe ulaşma oranı (hedefi olanlar önce), sonra net ciro
            .OrderByDescending(r => r.achievement.HasValue)
            .ThenByDescending(r => r.achievement ?? 0)
            .ThenByDescending(r => r.c.Net)
            .Select((r, i) => new StorePerformanceDto(
                i + 1, r.s.Id, r.s.Name, r.s.Code, r.s.IsActive,
                r.c.Net, r.c.Sales, r.c.Refunds, r.c.SaleCount, r.c.Items,
                r.c.SaleCount > 0 ? Math.Round(r.c.Sales / r.c.SaleCount, 2) : 0,
                r.target, r.achievement, r.expected, r.Pace, r.Remaining,
                r.p.Net, Change((currentComparable.GetValueOrDefault(r.s.Id) ?? StoreAgg.Empty).Net, r.p.Net),
                totalNet != 0 ? Math.Round(r.c.Net / totalNet * 100, 1) : 0))
            .ToList();

        // ── Toplamlar ─────────────────────────────────────────────────────
        var withTarget = rows.Where(r => r.TargetAmount is > 0).ToList();
        decimal? totalTarget = withTarget.Count > 0 ? withTarget.Sum(r => r.TargetAmount!.Value) : null;
        var saleCount = rows.Sum(r => r.SaleCount);
        var salesAmount = rows.Sum(r => r.SalesAmount);
        var prevNet = previous.Values.Sum(p => p.Net);
        var totals = new SalesPerformanceTotals(
            totalNet, salesAmount, rows.Sum(r => r.RefundAmount), saleCount,
            current.Values.Sum(c => c.RefundCount), rows.Sum(r => r.ItemsSold),
            saleCount > 0 ? Math.Round(salesAmount / saleCount, 2) : 0,
            totalTarget,
            totalTarget is > 0 ? Math.Round(withTarget.Sum(r => r.NetRevenue) / totalTarget.Value * 100, 1) : null,
            prevNet, Change(currentComparable.Values.Sum(c => c.Net), prevNet));

        // ── Günlük seyir (yerel gün) ──────────────────────────────────────
        var saleTimes = await _db.Sales.AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .Select(s => new { s.CreatedAt, s.FinalAmount })
            .ToListAsync(ct);
        var returnTimes = await _db.SaleReturns.AsNoTracking()
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc)
            .Select(r => new { r.CreatedAt, r.TotalAmount })
            .ToListAsync(ct);
        var daily = Enumerable.Range(0, days).Select(i =>
        {
            var d = from.AddDays(i);
            var daySales = saleTimes.Where(s => BusinessClock.ToLocal(s.CreatedAt).Date == d).ToList();
            var dayReturns = returnTimes.Where(r => BusinessClock.ToLocal(r.CreatedAt).Date == d).Sum(r => r.TotalAmount);
            return new DailyRevenueDto(DateOnly.FromDateTime(d), daySales.Sum(s => s.FinalAmount) - dayReturns, daySales.Count);
        }).ToList();

        // ── En çok satanlar (adet) ────────────────────────────────────────
        var top = await _db.SaleItems.AsNoTracking()
            .Where(i => i.Sale.CreatedAt >= fromUtc && i.Sale.CreatedAt < toUtc)
            .GroupBy(i => new { i.VariantId, i.ProductNameSnapshot, i.ColorSnapshot, i.SizeSnapshot })
            .Select(g => new
            {
                g.Key.VariantId, g.Key.ProductNameSnapshot, g.Key.ColorSnapshot, g.Key.SizeSnapshot,
                Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue)
            .Take(10)
            .ToListAsync(ct);
        var topProducts = top.Select(t => new TopProductDto(t.VariantId, t.ProductNameSnapshot, t.ColorSnapshot,
            t.SizeSnapshot, t.Quantity, t.Revenue)).ToList();

        return new SalesPerformanceDto(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), days, elapsed, totals, rows, daily, topProducts);
    }

    private sealed record StoreAgg(decimal Sales, decimal Refunds, int SaleCount, int RefundCount, int Items)
    {
        public static readonly StoreAgg Empty = new(0, 0, 0, 0, 0);
        public decimal Net => Sales - Refunds;
    }

    private async Task<Dictionary<int, StoreAgg>> AggregateAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .GroupBy(s => s.StoreId)
            .Select(g => new { StoreId = g.Key, Amount = g.Sum(s => s.FinalAmount), Count = g.Count() })
            .ToListAsync(ct);
        var items = await _db.SaleItems.AsNoTracking()
            .Where(i => i.Sale.CreatedAt >= fromUtc && i.Sale.CreatedAt < toUtc)
            .GroupBy(i => i.Sale.StoreId)
            .Select(g => new { StoreId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.StoreId, x => x.Quantity, ct);
        var returns = await _db.SaleReturns.AsNoTracking()
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc)
            .GroupBy(r => r.StoreId)
            .Select(g => new { StoreId = g.Key, Amount = g.Sum(r => r.TotalAmount), Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<int, StoreAgg>();
        foreach (var s in sales) result[s.StoreId] = new StoreAgg(s.Amount, 0, s.Count, 0, items.GetValueOrDefault(s.StoreId));
        foreach (var r in returns)
        {
            var e = result.GetValueOrDefault(r.StoreId) ?? StoreAgg.Empty;
            result[r.StoreId] = e with { Refunds = r.Amount, RefundCount = r.Count };
        }
        return result;
    }

    /// <summary>Hedef temposu: olması gereken ilerlemeye göre (±5 puan tolerans).</summary>
    private static string Pace(decimal? achievement, decimal? expected)
    {
        if (achievement is null || expected is null) return "Hedef yok";
        if (achievement >= 100) return "Hedef tamam";
        if (expected == 0) return "Başlamadı";
        var diff = achievement.Value - expected.Value;
        return diff >= 5 ? "Önde" : diff <= -5 ? "Geride" : "Hedefte";
    }

    private static decimal? Change(decimal current, decimal previous)
        => previous != 0 ? Math.Round((current - previous) / Math.Abs(previous) * 100, 1) : null;
}

/// <summary>Aylık hedef girişi (Admin). Tutar 0 ya da boş → o ayın hedefi kaldırılır.</summary>
public record SetStoreSalesTargetCommand(int StoreId, int Year, int Month, decimal? TargetAmount) : IRequest<StoreTargetDto?>;

public record StoreTargetDto(int StoreId, string StoreName, string StoreCode, int Year, int Month, decimal? TargetAmount);

public record GetStoreTargetsQuery(int Year, int Month) : IRequest<List<StoreTargetDto>>;

public class StoreSalesTargetHandlers :
    IRequestHandler<SetStoreSalesTargetCommand, StoreTargetDto?>,
    IRequestHandler<GetStoreTargetsQuery, List<StoreTargetDto>>
{
    public const decimal MaxTarget = 1_000_000_000m;
    private readonly IApplicationDbContext _db;
    public StoreSalesTargetHandlers(IApplicationDbContext db) => _db = db;

    public async Task<StoreTargetDto?> Handle(SetStoreSalesTargetCommand c, CancellationToken ct)
    {
        if (c.Month is < 1 or > 12 || c.Year is < 2020 or > 2100)
            throw new InvalidOperationException("Geçersiz ay/yıl.");
        if (c.TargetAmount is < 0 or > MaxTarget)
            throw new InvalidOperationException("Hedef 0 ile 1.000.000.000 TL arasında olmalıdır.");
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == c.StoreId, ct)
                    ?? throw new KeyNotFoundException("Mağaza bulunamadı.");

        var existing = await _db.StoreSalesTargets
            .FirstOrDefaultAsync(t => t.StoreId == c.StoreId && t.Year == c.Year && t.Month == c.Month, ct);

        if (c.TargetAmount is null or 0)
        {
            if (existing is not null) _db.StoreSalesTargets.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return new StoreTargetDto(store.Id, store.Name, store.Code, c.Year, c.Month, null);
        }

        if (existing is null)
            _db.StoreSalesTargets.Add(existing = new StoreSalesTarget { StoreId = c.StoreId, Year = c.Year, Month = c.Month });
        existing.TargetAmount = Math.Round(c.TargetAmount.Value, 2);
        await _db.SaveChangesAsync(ct);
        return new StoreTargetDto(store.Id, store.Name, store.Code, c.Year, c.Month, existing.TargetAmount);
    }

    public async Task<List<StoreTargetDto>> Handle(GetStoreTargetsQuery q, CancellationToken ct)
    {
        var targets = await _db.StoreSalesTargets.AsNoTracking()
            .Where(t => t.Year == q.Year && t.Month == q.Month)
            .ToDictionaryAsync(t => t.StoreId, t => t.TargetAmount, ct);
        var stores = await _db.Stores.AsNoTracking()
            .Select(s => new { s.Id, s.Name, s.Code, s.IsActive })
            .ToListAsync(ct);
        return stores
            .Where(s => s.IsActive || targets.ContainsKey(s.Id))
            .OrderBy(s => s.Name)
            .Select(s => new StoreTargetDto(s.Id, s.Name, s.Code, q.Year, q.Month,
                targets.TryGetValue(s.Id, out var t) ? t : null))
            .ToList();
    }
}
