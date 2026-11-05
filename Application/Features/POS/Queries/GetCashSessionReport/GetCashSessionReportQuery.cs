using MediatR;

namespace Application.Features.POS.Queries.GetCashSessionReport;

/// <summary>
/// WPOS "Kasa Raporu" — açık oturumun avans + nakit satış toplamına
/// göre beklenen kasa tutarını gösterir (ara rapor, sayaç sıfırlanmaz).
/// </summary>
/// <param name="CashierId">
/// Raporu alınacak kasiyer (müdür/Admin, kendi mağazasındaki kasiyer için).
/// Boşsa isteği yapanın kendi oturumu. Eskiden parametre yoktu: uç sadece
/// müdüre açık olduğu halde müdürün KENDİ oturumunu arıyordu, kasiyerin
/// beklenen kasa tutarını hiç gösteremiyordu.
/// </param>
public record GetCashSessionReportQuery(string? CashierId = null) : IRequest<CashSessionReportDto>;

public record CashSessionReportDto(
    int SessionId,
    string CashierId,
    decimal OpeningFloat,
    decimal CashSalesTotal,
    decimal ExpectedCashAmount,
    int TransactionCount,
    DateTime OpenedAt,
    DateTime ReportGeneratedAt,
    decimal CashRefundTotal = 0,  // oturumda kasadan verilen nakit iade (beklenenden düşülür)
    decimal GiftCardSoldCash = 0  // oturumda hediye kartı satışından alınan nakit (beklenene eklenir)
);