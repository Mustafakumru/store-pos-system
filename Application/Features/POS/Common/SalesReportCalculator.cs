using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Common;

public record SalesReportData(
    decimal TotalSalesAmount,
    decimal TotalRefundsAmount,
    decimal NetAmount,
    decimal CashTotal,
    decimal CardTotal,
    decimal GiftCardTotal,
    int TransactionCount,
    int RefundCount,
    // ── İade ödeme kırılımı (iade defterinden) ─────────────
    decimal CashRefundTotal = 0,
    decimal CardRefundTotal = 0,
    decimal GiftCardRefundTotal = 0,
    decimal ExchangeCreditTotal = 0,
    // ── Hediye kartı satışı (EMANET - satış/net toplamlarına dahil DEĞİL) ──
    decimal GiftCardSoldTotal = 0,
    decimal GiftCardSoldCash = 0,
    decimal GiftCardSoldCard = 0
);

/// <summary>
/// X/Z raporu, Mali rapor ve panel satış özeti için ORTAK hesap.
///
/// Satış: dönemde bu mağazada kesilen fişlerin FinalAmount toplamı (değişim
/// satışları tam tutarıyla). Ödeme kırılımı SalePayment'tan - kayıtlı tutarlar
/// para üstü düşülmüş tutarlardır; "Değişim Kredisi" ödemesi nakit/karta sayılmaz.
///
/// İade: iade DEFTERİNDEN (SaleReturn) - iadeyi YAPAN mağaza ve gerçek iade
/// anına göre. Mağazalar arası iade, iadenin yapıldığı mağazadan düşer.
///
/// Net = Satış − İade. Değişimde iade kredisi hem satışta (ödeme) hem iadede
/// (ExchangeCredit) yer aldığı için net doğru çıkar: 100 TL iade + 150 TL alım
/// = 150 − 100 = +50 TL.
/// </summary>
public static class SalesReportCalculator
{
    public static async Task<SalesReportData> CalculateAsync(
        IApplicationDbContext context,
        int? storeId,            // null = tüm mağazalar (Admin satış geçmişi)
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        // ── Satışlar ───────────────────────────────────────────
        var sales = context.Sales
            .Where(s => (storeId == null || s.StoreId == storeId)
                     && s.CreatedAt >= fromDate
                     && s.CreatedAt < toDate);

        var totalSalesAmount = await sales.SumAsync(s => (decimal?)s.FinalAmount, cancellationToken) ?? 0;
        var transactionCount = await sales.CountAsync(cancellationToken);

        var paymentTotals = await sales
            .SelectMany(s => s.Payments)
            .GroupBy(p => p.PaymentType)
            .Select(g => new { Type = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        decimal Paid(PaymentType t) => paymentTotals.Where(p => p.Type == t).Sum(p => p.Amount);

        // ── İadeler (defter) ───────────────────────────────────
        var returns = context.SaleReturns
            .Where(r => (storeId == null || r.StoreId == storeId)
                     && r.CreatedAt >= fromDate
                     && r.CreatedAt < toDate);

        var totalRefundsAmount = await returns.SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0;
        var refundCount = await returns.CountAsync(cancellationToken);

        var payoutTotals = await returns
            .SelectMany(r => r.Payouts)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        decimal PaidOut(RefundMethod m) => payoutTotals.Where(p => p.Method == m).Sum(p => p.Amount);

        // ── Hediye kartı satışı (emanet): ciro değil, ama kasaya para girer ──
        var gcSales = context.GiftCardSales
            .Where(g => (storeId == null || g.StoreId == storeId)
                     && g.CreatedAt >= fromDate
                     && g.CreatedAt < toDate);
        var gcTotal = await gcSales.SumAsync(g => (decimal?)g.TotalAmount, cancellationToken) ?? 0;
        var gcPayments = await gcSales
            .SelectMany(g => g.Payments)
            .GroupBy(p => p.PaymentType)
            .Select(g => new { Type = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);
        decimal GcPaid(PaymentType t) => gcPayments.Where(p => p.Type == t).Sum(p => p.Amount);

        return new SalesReportData(
            TotalSalesAmount: totalSalesAmount,
            TotalRefundsAmount: totalRefundsAmount,
            NetAmount: totalSalesAmount - totalRefundsAmount,
            CashTotal: Paid(PaymentType.Cash),
            CardTotal: Paid(PaymentType.Card),
            GiftCardTotal: Paid(PaymentType.GiftCard),
            TransactionCount: transactionCount,
            RefundCount: refundCount,
            CashRefundTotal: PaidOut(RefundMethod.Cash),
            CardRefundTotal: PaidOut(RefundMethod.Card),
            GiftCardRefundTotal: PaidOut(RefundMethod.GiftCard),
            ExchangeCreditTotal: PaidOut(RefundMethod.ExchangeCredit),
            GiftCardSoldTotal: gcTotal,
            GiftCardSoldCash: GcPaid(PaymentType.Cash),
            GiftCardSoldCard: GcPaid(PaymentType.Card));
    }
}
