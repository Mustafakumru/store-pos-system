using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetCashSessions;

/// <summary>
/// Mağazanın kasa oturumları (yönetici görünümü).
/// Açılışı [From, To) aralığında olan oturumlar + hâlâ açık olan tüm oturumlar.
/// Beklenen nakit = avans + oturum süresince o kasiyerin aldığı nakit ödemeler.
/// NOT: Nakit iadeler ödeme tipi bazında izlenmediği için beklenen tutardan
/// düşülmez (GetCashSessionReport ile aynı kabul).
/// </summary>
public record GetCashSessionsQuery(
    int StoreId,
    DateTime From,
    DateTime To
) : IRequest<List<CashSessionListItemDto>>;

public record CashSessionListItemDto(
    int Id,
    string CashierId,
    string? CashierName,
    string Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningFloat,
    decimal CashSalesTotal,
    decimal ExpectedCashAmount,
    decimal? ClosingAmount,
    decimal? Difference,          // teslim − beklenen (eksi = kasa açığı)
    int TransactionCount,
    string? OpeningApprovedByUserId,
    string? OpeningApprovedByName,
    string? ClosingApprovedByUserId,
    string? ClosingApprovedByName,
    decimal CashRefundTotal = 0,  // oturumda verilen nakit iade (beklenenden düşüldü)
    decimal GiftCardSoldCash = 0  // oturumda hediye kartı satışından alınan nakit (beklenene eklendi)
);

public class GetCashSessionsQueryHandler
    : IRequestHandler<GetCashSessionsQuery, List<CashSessionListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCashSessionsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<CashSessionListItemDto>> Handle(
        GetCashSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var from = ToUtc(request.From);
        var to = ToUtc(request.To);
        if (to <= from)
            throw new InvalidOperationException("Bitiş tarihi başlangıçtan sonra olmalıdır.");
        if (to - from > TimeSpan.FromDays(93))
            throw new InvalidOperationException("En fazla 3 aylık dönem sorgulanabilir.");

        var sessions = await _context.CashDrawerSessions
            .AsNoTracking()
            .Where(s => s.StoreId == request.StoreId
                     && ((s.OpenedAt >= from && s.OpenedAt < to)
                         || s.Status == CashSessionStatus.Open))
            .OrderByDescending(s => s.OpenedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0) return new();

        var now = DateTime.UtcNow;
        var minOpened = sessions.Min(s => s.OpenedAt);
        var cashierIds = sessions.Select(s => s.CashierId).Distinct().ToList();

        // İlgili kasiyerlerin bu aralıktaki satışları (tek sorgu, eşleştirme bellekte)
        var sales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.StoreId == request.StoreId
                     && cashierIds.Contains(s.CashierId)
                     && s.CreatedAt >= minOpened)
            .Select(s => new
            {
                s.CashierId,
                s.CreatedAt,
                Cash = s.Payments
                    .Where(p => p.PaymentType == PaymentType.Cash)
                    .Sum(p => (decimal?)p.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        // Aynı kasiyerlerin kasadan verdiği nakit iadeler (iade defteri)
        var cashRefunds = await _context.SaleReturnPayouts
            .AsNoTracking()
            .Where(p => p.Method == RefundMethod.Cash
                     && p.SaleReturn.StoreId == request.StoreId
                     && cashierIds.Contains(p.SaleReturn.CashierId)
                     && p.SaleReturn.CreatedAt >= minOpened)
            .Select(p => new { p.SaleReturn.CashierId, p.SaleReturn.CreatedAt, p.Amount })
            .ToListAsync(cancellationToken);

        // Hediye kartı satışlarının nakit tahsilatı (emanet ama kasaya girer)
        var giftCardCash = await _context.GiftCardSales
            .AsNoTracking()
            .Where(g => g.StoreId == request.StoreId
                     && cashierIds.Contains(g.CashierId)
                     && g.CreatedAt >= minOpened)
            .Select(g => new
            {
                g.CashierId,
                g.CreatedAt,
                Cash = g.Payments.Where(p => p.PaymentType == PaymentType.Cash).Sum(p => (decimal?)p.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        return sessions.Select(s =>
        {
            var end = s.ClosedAt ?? now;
            var inSession = sales.Where(x => x.CashierId == s.CashierId
                                          && x.CreatedAt >= s.OpenedAt
                                          && x.CreatedAt < end).ToList();
            var cash = inSession.Sum(x => x.Cash);
            var refunded = cashRefunds.Where(r => r.CashierId == s.CashierId
                                               && r.CreatedAt >= s.OpenedAt
                                               && r.CreatedAt < end).Sum(r => r.Amount);
            var gcCash = giftCardCash.Where(g => g.CashierId == s.CashierId
                                              && g.CreatedAt >= s.OpenedAt
                                              && g.CreatedAt < end).Sum(g => g.Cash);
            var expected = s.OpeningFloat + cash - refunded + gcCash;

            return new CashSessionListItemDto(
                Id: s.Id,
                CashierId: s.CashierId,
                CashierName: null,
                Status: s.Status.ToString(),
                OpenedAt: s.OpenedAt,
                ClosedAt: s.ClosedAt,
                OpeningFloat: s.OpeningFloat,
                CashSalesTotal: cash,
                ExpectedCashAmount: expected,
                ClosingAmount: s.ClosingAmount,
                Difference: s.ClosingAmount.HasValue ? s.ClosingAmount.Value - expected : null,
                TransactionCount: inSession.Count,
                OpeningApprovedByUserId: s.OpeningApprovedByUserId,
                OpeningApprovedByName: null,
                ClosingApprovedByUserId: s.ClosingApprovedByUserId,
                ClosingApprovedByName: null,
                CashRefundTotal: refunded,
                GiftCardSoldCash: gcCash);
        }).ToList();
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
