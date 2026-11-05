using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetFinancialReport;

public class GetFinancialReportQueryHandler
    : IRequestHandler<GetFinancialReportQuery, FinancialReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetFinancialReportQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<FinancialReportDto> Handle(
        GetFinancialReportQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        // ── Parametre doğrulama ────────────────────────────────
        var hasZRange = request.FromZNumber.HasValue && request.ToZNumber.HasValue;
        var hasDateRange = request.FromDate.HasValue && request.ToDate.HasValue;

        if (!hasZRange && !hasDateRange)
            throw new InvalidOperationException(
                "Z no aralığı (fromZNumber + toZNumber) veya tarih aralığı " +
                "(fromDate + toDate) girilmelidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        // ── Z raporlarını filtrele ─────────────────────────────
        var query = _context.ZReports
            .AsNoTracking()
            .Where(z => z.StoreId == storeId);

        if (hasZRange)
        {
            query = query.Where(z => z.ReportNumber >= request.FromZNumber!.Value
                                  && z.ReportNumber <= request.ToZNumber!.Value);
        }
        else
        {
            query = query.Where(z => z.ToDate >= request.FromDate!.Value
                                  && z.ToDate <= request.ToDate!.Value);
        }

        var zReports = await query
            .OrderBy(z => z.ReportNumber)
            .ToListAsync(cancellationToken);

        if (zReports.Count == 0)
            throw new KeyNotFoundException("Belirtilen aralıkta Z raporu bulunamadı.");

        // ── Özet hesapla ───────────────────────────────────────
        return new FinancialReportDto(
            FromZNumber: zReports.First().ReportNumber,
            ToZNumber: zReports.Last().ReportNumber,
            FromDate: zReports.First().FromDate,
            ToDate: zReports.Last().ToDate,

            TotalSalesAmount: zReports.Sum(z => z.TotalSalesAmount),
            TotalRefundsAmount: zReports.Sum(z => z.TotalRefundsAmount),
            NetAmount: zReports.Sum(z => z.NetAmount),

            CashTotal: zReports.Sum(z => z.CashTotal),
            CardTotal: zReports.Sum(z => z.CardTotal),
            GiftCardTotal: zReports.Sum(z => z.GiftCardTotal),

            TransactionCount: zReports.Sum(z => z.TransactionCount),
            RefundCount: zReports.Sum(z => z.RefundCount),
            ZReportCount: zReports.Count,

            ReportGeneratedAt: DateTime.UtcNow,
            CashRefundTotal: zReports.Sum(z => z.CashRefundTotal),
            CardRefundTotal: zReports.Sum(z => z.CardRefundTotal),
            GiftCardSoldTotal: zReports.Sum(z => z.GiftCardSoldTotal),
            GiftCardSoldCash: zReports.Sum(z => z.GiftCardSoldCash),
            GiftCardSoldCard: zReports.Sum(z => z.GiftCardSoldCard));
    }
}