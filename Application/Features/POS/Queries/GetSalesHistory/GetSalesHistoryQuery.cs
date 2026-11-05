using Application.Common.Interfaces;
using Application.Features.POS.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetSalesHistory;

/// <summary>
/// Satış geçmişi + dönem özeti.
/// Dönem: From/To (UTC anları; panel yerel gün başı/sonunu gönderir) verilirse
/// o aralık, yoksa Date'in UTC günü, o da yoksa bugün (UTC).
/// </summary>
public record GetSalesHistoryQuery(
    int? StoreId = null,     // Admin: filtreli/null=tümü, StoreManager: kendi mağazası
    DateTime? Date = null,
    int Page = 1,
    int PageSize = 20,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<SalesHistoryResult>;

public record SalesHistoryResult(
    int TotalCount,
    int Page,
    int PageSize,
    DateTime From,
    DateTime To,
    SalesSummaryDto Summary,
    List<SaleDto> Items
);

/// <summary>
/// Dönem özeti - X/Z raporlarıyla AYNI hesap (SalesReportCalculator):
/// iadeler, satışın değil İADENİN yapıldığı tarihe göre ve gerçek iade
/// tutarı (RefundedAmount) üzerinden düşülür; ödeme kırılımı ödeme
/// kayıtlarından gelir (karma ödemeli satışlar da doğru dağılır).
/// </summary>
public record SalesSummaryDto(
    int TotalSales,
    int TotalItems,
    decimal TotalRevenue,
    decimal TotalDiscount,
    int RefundCount,
    decimal RefundTotal,
    decimal NetRevenue,
    decimal CashTotal,
    decimal CardTotal,
    decimal GiftCardTotal,
    decimal CashRefundTotal = 0,
    decimal CardRefundTotal = 0,
    decimal ExchangeCreditTotal = 0,
    decimal GiftCardSoldTotal = 0,   // hediye kartı satışı (EMANET - ciroya dahil değil)
    decimal GiftCardSoldCash = 0,
    decimal GiftCardSoldCard = 0
);

public record SaleDto(
    int Id,
    string ReceiptNumber,
    string CashierId,
    string? CashierName,
    DateTime SaleDate,
    string PaymentType,
    string Status,
    decimal FinalAmount,
    int ItemCount,
    bool HasNegativeStockItem
);

public class GetSalesHistoryQueryHandler
    : IRequestHandler<GetSalesHistoryQuery, SalesHistoryResult>
{
    private const int MaxPageSize = 500;
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(93);

    private readonly IApplicationDbContext _context;

    public GetSalesHistoryQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<SalesHistoryResult> Handle(
        GetSalesHistoryQuery request,
        CancellationToken cancellationToken)
    {
        DateTime from, to;
        if (request.From.HasValue && request.To.HasValue)
        {
            from = ToUtc(request.From.Value);
            to = ToUtc(request.To.Value);
        }
        else
        {
            // Tek gün: yerel iş gününün UTC aralığı
            (from, to) = Application.Common.BusinessClock.DayRangeUtc(
                request.Date?.Date ?? Application.Common.BusinessClock.Today);
        }

        if (to <= from)
            throw new InvalidOperationException("Bitiş tarihi başlangıçtan sonra olmalıdır.");
        if (to - from > MaxRange)
            throw new InvalidOperationException("En fazla 3 aylık dönem sorgulanabilir.");

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = _context.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to);

        if (request.StoreId.HasValue)
            query = query.Where(s => s.StoreId == request.StoreId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalItems = await query.SelectMany(s => s.Items).SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;
        var totalDiscount = await query.SumAsync(s => (decimal?)s.DiscountAmount, cancellationToken) ?? 0;

        var report = await SalesReportCalculator.CalculateAsync(
            _context, request.StoreId, from, to, cancellationToken);

        var summary = new SalesSummaryDto(
            TotalSales: report.TransactionCount,
            TotalItems: totalItems,
            TotalRevenue: report.TotalSalesAmount,
            TotalDiscount: totalDiscount,
            RefundCount: report.RefundCount,
            RefundTotal: report.TotalRefundsAmount,
            NetRevenue: report.NetAmount,
            CashTotal: report.CashTotal,
            CardTotal: report.CardTotal,
            GiftCardTotal: report.GiftCardTotal,
            CashRefundTotal: report.CashRefundTotal,
            CardRefundTotal: report.CardRefundTotal,
            ExchangeCreditTotal: report.ExchangeCreditTotal,
            GiftCardSoldTotal: report.GiftCardSoldTotal,
            GiftCardSoldCash: report.GiftCardSoldCash,
            GiftCardSoldCard: report.GiftCardSoldCard);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SaleDto(
                s.Id,
                s.ReceiptNumber,
                s.CashierId,
                null, // kasiyer adı WebAPI katmanında (Identity) doldurulur
                s.CreatedAt,
                s.PaymentType.ToString(),
                s.Status.ToString(),
                s.FinalAmount,
                s.Items.Sum(i => i.Quantity),
                s.HasNegativeStockItem))
            .ToListAsync(cancellationToken);

        return new SalesHistoryResult(totalCount, page, pageSize, from, to, summary, items);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
