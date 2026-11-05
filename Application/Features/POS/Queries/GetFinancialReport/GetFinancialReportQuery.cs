using MediatR;

namespace Application.Features.POS.Queries.GetFinancialReport;

/// <summary>
/// WPOS "Mali Rapor" — Z no aralığı veya tarih aralığına göre
/// kaydedilmiş Z raporlarını özetler.
/// İkisinden biri zorunlu: (FromZNumber + ToZNumber) veya (FromDate + ToDate).
/// </summary>
public record GetFinancialReportQuery(
    int? FromZNumber = null,
    int? ToZNumber = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<FinancialReportDto>;

public record FinancialReportDto(
    // ── Rapor Aralığı ──────────────────────────────────────────
    int FromZNumber,
    int ToZNumber,
    DateTime FromDate,
    DateTime ToDate,

    // ── Özet Tutarlar ──────────────────────────────────────────
    decimal TotalSalesAmount,
    decimal TotalRefundsAmount,
    decimal NetAmount,

    // ── Ödeme Tipi Kırılımı ────────────────────────────────────
    decimal CashTotal,
    decimal CardTotal,
    decimal GiftCardTotal,

    // ── İşlem Sayıları ─────────────────────────────────────────
    int TransactionCount,
    int RefundCount,
    int ZReportCount,

    DateTime ReportGeneratedAt,
    decimal CashRefundTotal = 0,
    decimal CardRefundTotal = 0,
    decimal GiftCardSoldTotal = 0,
    decimal GiftCardSoldCash = 0,
    decimal GiftCardSoldCard = 0
);