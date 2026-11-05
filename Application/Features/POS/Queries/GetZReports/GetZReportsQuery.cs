using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetZReports;

/// <summary>
/// Mağazanın kaydedilmiş Z raporlarını listeler (en yeni önce).
/// From/To verilirse raporun alındığı an (ToDate) bu aralıkta olanlar döner.
/// </summary>
public record GetZReportsQuery(
    int StoreId,
    DateTime? From = null,
    DateTime? To = null,
    int Take = 100
) : IRequest<List<ZReportListItemDto>>;

public record ZReportListItemDto(
    int Id,
    int ReportNumber,
    DateTime FromDate,
    DateTime ToDate,
    decimal TotalSalesAmount,
    decimal TotalRefundsAmount,
    decimal NetAmount,
    decimal CashTotal,
    decimal CardTotal,
    decimal GiftCardTotal,
    int TransactionCount,
    int RefundCount,
    string CashierId,
    string? CashierName,
    decimal CashRefundTotal = 0,
    decimal CardRefundTotal = 0,
    decimal GiftCardSoldTotal = 0,
    decimal GiftCardSoldCash = 0
);

public class GetZReportsQueryHandler
    : IRequestHandler<GetZReportsQuery, List<ZReportListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetZReportsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<ZReportListItemDto>> Handle(
        GetZReportsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ZReports
            .AsNoTracking()
            .Where(z => z.StoreId == request.StoreId);

        if (request.From.HasValue)
            query = query.Where(z => z.ToDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(z => z.ToDate < request.To.Value);

        return await query
            .OrderByDescending(z => z.ReportNumber)
            .Take(Math.Clamp(request.Take, 1, 500))
            .Select(z => new ZReportListItemDto(
                z.Id,
                z.ReportNumber,
                z.FromDate,
                z.ToDate,
                z.TotalSalesAmount,
                z.TotalRefundsAmount,
                z.NetAmount,
                z.CashTotal,
                z.CardTotal,
                z.GiftCardTotal,
                z.TransactionCount,
                z.RefundCount,
                z.CashierId,
                null, // kasiyer adı WebAPI katmanında (Identity) doldurulur
                z.CashRefundTotal,
                z.CardRefundTotal,
                z.GiftCardSoldTotal,
                z.GiftCardSoldCash))
            .ToListAsync(cancellationToken);
    }
}
