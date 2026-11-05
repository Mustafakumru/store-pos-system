using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetSalesBreakdown;

/// <summary>
/// Satış dönemi kırılımı: saatlik, günlük ve kasiyer bazında.
/// From/To UTC anlarıdır (panel yerel gün başı/sonunu gönderir).
/// TzOffsetMinutes: istemcinin UTC'ye göre farkı (Türkiye: +180). Saat ve gün
/// grupları bu farkla YEREL zamana göre yapılır.
/// Tutar = satışların FinalAmount toplamı (iadeler düşülmemiş brüt satış).
/// </summary>
public record GetSalesBreakdownQuery(
    int? StoreId,
    DateTime From,
    DateTime To,
    int TzOffsetMinutes = 0
) : IRequest<SalesBreakdownDto>;

public record SalesBreakdownDto(
    List<HourlyBucketDto> Hourly,
    List<DailyBucketDto> Daily,
    List<CashierBreakdownDto> Cashiers
);

public record HourlyBucketDto(int Hour, int SaleCount, decimal Revenue);

public record DailyBucketDto(DateOnly Date, int SaleCount, decimal Revenue);

public record CashierBreakdownDto(
    string CashierId,
    string? CashierName,
    int SaleCount,
    int ItemCount,
    decimal Revenue,
    decimal AverageBasket
);

public class GetSalesBreakdownQueryHandler
    : IRequestHandler<GetSalesBreakdownQuery, SalesBreakdownDto>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(93);
    private readonly IApplicationDbContext _context;

    public GetSalesBreakdownQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<SalesBreakdownDto> Handle(
        GetSalesBreakdownQuery request,
        CancellationToken cancellationToken)
    {
        var from = ToUtc(request.From);
        var to = ToUtc(request.To);
        if (to <= from)
            throw new InvalidOperationException("Bitiş tarihi başlangıçtan sonra olmalıdır.");
        if (to - from > MaxRange)
            throw new InvalidOperationException("En fazla 3 aylık dönem sorgulanabilir.");

        // Saat dilimi farkı makul aralıkta olmalı (±14 saat)
        var offset = TimeSpan.FromMinutes(Math.Clamp(request.TzOffsetMinutes, -14 * 60, 14 * 60));

        var query = _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to);
        if (request.StoreId.HasValue)
            query = query.Where(s => s.StoreId == request.StoreId.Value);

        var rows = await query
            .Select(s => new
            {
                s.CreatedAt,
                s.CashierId,
                s.FinalAmount,
                Items = s.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);

        var hourly = rows
            .GroupBy(r => (r.CreatedAt + offset).Hour)
            .Select(g => new HourlyBucketDto(g.Key, g.Count(), g.Sum(r => r.FinalAmount)))
            .OrderBy(h => h.Hour)
            .ToList();

        var daily = rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAt + offset))
            .Select(g => new DailyBucketDto(g.Key, g.Count(), g.Sum(r => r.FinalAmount)))
            .OrderBy(d => d.Date)
            .ToList();

        var cashiers = rows
            .GroupBy(r => r.CashierId)
            .Select(g =>
            {
                var revenue = g.Sum(r => r.FinalAmount);
                var count = g.Count();
                return new CashierBreakdownDto(
                    g.Key, null, count, g.Sum(r => r.Items), revenue,
                    count == 0 ? 0 : Math.Round(revenue / count, 2));
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        return new SalesBreakdownDto(hourly, daily, cashiers);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
