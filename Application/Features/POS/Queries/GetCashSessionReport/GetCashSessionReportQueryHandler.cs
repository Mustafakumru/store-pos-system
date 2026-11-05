using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetCashSessionReport;

public class GetCashSessionReportQueryHandler
    : IRequestHandler<GetCashSessionReportQuery, CashSessionReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCashSessionReportQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CashSessionReportDto> Handle(
        GetCashSessionReportQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);
        // Mağaza kapsamı aşağıdaki StoreId filtresiyle korunur: başka mağazanın
        // kasiyeri verilse de oturumu bulunamaz.
        var cashierId = string.IsNullOrWhiteSpace(request.CashierId)
            ? _currentUser.UserId
            : request.CashierId.Trim();

        var session = await _context.CashDrawerSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StoreId == storeId
                                   && s.CashierId == cashierId
                                   && s.Status == CashSessionStatus.Open,
                                  cancellationToken)
            ?? throw new InvalidOperationException(
                cashierId == _currentUser.UserId
                    ? "Açık bir kasa oturumunuz bulunamadı."
                    : "Bu kasiyerin bu mağazada açık kasa oturumu yok.");

        // Oturum açıldığından beri alınan nakit (para üstü düşülmüş kayıtlı tutarlar)
        var cashSalesTotal = await _context.SalePayments
            .Where(p => p.PaymentType == PaymentType.Cash
                     && p.Sale.StoreId == storeId
                     && p.Sale.CashierId == cashierId
                     && p.Sale.CreatedAt >= session.OpenedAt)
            .SumAsync(p => p.Amount, cancellationToken);

        // Bu kasiyerin oturumda kasadan verdiği nakit iadeler (iade defteri)
        var cashRefundTotal = await _context.SaleReturnPayouts
            .Where(p => p.Method == RefundMethod.Cash
                     && p.SaleReturn.StoreId == storeId
                     && p.SaleReturn.CashierId == cashierId
                     && p.SaleReturn.CreatedAt >= session.OpenedAt)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        // Bu kasiyerin oturumda sattığı hediye kartlarının NAKİT tahsilatı (emanet ama kasada)
        var giftCardCash = await _context.GiftCardSales
            .Where(g => g.StoreId == storeId
                     && g.CashierId == cashierId
                     && g.CreatedAt >= session.OpenedAt)
            .SelectMany(g => g.Payments)
            .Where(p => p.PaymentType == PaymentType.Cash)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        var transactionCount = await _context.Sales
            .Where(s => s.StoreId == storeId
                     && s.CashierId == cashierId
                     && s.CreatedAt >= session.OpenedAt)
            .CountAsync(cancellationToken);

        return new CashSessionReportDto(
            SessionId: session.Id,
            CashierId: session.CashierId,
            OpeningFloat: session.OpeningFloat,
            CashSalesTotal: cashSalesTotal,
            ExpectedCashAmount: session.OpeningFloat + cashSalesTotal - cashRefundTotal + giftCardCash,
            TransactionCount: transactionCount,
            OpenedAt: session.OpenedAt,
            ReportGeneratedAt: DateTime.UtcNow,
            CashRefundTotal: cashRefundTotal,
            GiftCardSoldCash: giftCardCash);
    }
}