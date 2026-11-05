using Application.Common.Interfaces;
using Application.Features.POS.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetXReport;

public class GetXReportQueryHandler
    : IRequestHandler<GetXReportQuery, XReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetXReportQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<XReportDto> Handle(
        GetXReportQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        var lastZReport = await _context.ZReports
            .Where(z => z.StoreId == storeId)
            .OrderByDescending(z => z.ReportNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var fromDate = lastZReport?.ToDate ?? DateTime.MinValue;
        var toDate = DateTime.UtcNow;

        var data = await SalesReportCalculator.CalculateAsync(
            _context, storeId, fromDate, toDate, cancellationToken);

        var full = new XReportDto(
            FromDate: fromDate,
            ToDate: toDate,
            TotalSalesAmount: data.TotalSalesAmount,
            TotalRefundsAmount: data.TotalRefundsAmount,
            NetAmount: data.NetAmount,
            CashTotal: data.CashTotal,
            CardTotal: data.CardTotal,
            GiftCardTotal: data.GiftCardTotal,
            TransactionCount: data.TransactionCount,
            RefundCount: data.RefundCount,
            LastZReportNumber: lastZReport?.ReportNumber,
            CashRefundTotal: data.CashRefundTotal,
            CardRefundTotal: data.CardRefundTotal,
            NetCash: data.CashTotal - data.CashRefundTotal + data.GiftCardSoldCash,
            GiftCardSoldTotal: data.GiftCardSoldTotal,
            GiftCardSoldCash: data.GiftCardSoldCash,
            GiftCardSoldCard: data.GiftCardSoldCard
        );

        if (request.IncludeCashDetails) return full;

        // Kör sayım: kasiyere nakdi ortaya çıkaracak hiçbir toplam gönderilmez
        return full with
        {
            TotalSalesAmount = 0,
            TotalRefundsAmount = 0,
            NetAmount = 0,
            CashTotal = 0,
            GiftCardTotal = 0,
            CashRefundTotal = 0,
            NetCash = 0,
            GiftCardSoldTotal = 0,
            GiftCardSoldCash = 0,
            CashDetailsHidden = true
        };
    }
}