using MediatR;

namespace Application.Features.POS.Queries.GetXReport;

/// <summary>
/// WPOS "X Raporu" — son Z raporundan (veya hiç yoksa başlangıçtan)
/// bu yana özet. Sayaç sıfırlanmaz, hiçbir şey kaydedilmez.
/// </summary>
/// <param name="IncludeCashDetails">
/// false (kasiyer): nakit ve ciro alanları sıfırlanır. Teslim KÖR sayımdır;
/// kasiyer beklenen nakdi (ya da ciro − kart − hediye kartı ile hesaplanabilecek
/// tutarı) görürse sayımı ona göre "tutturabilir". Kart toplamları (POS cihazı
/// mutabakatı) ve işlem sayıları görünür kalır.
/// </param>
public record GetXReportQuery(bool IncludeCashDetails = true) : IRequest<XReportDto>;

public record XReportDto(
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
    int? LastZReportNumber,
    decimal CashRefundTotal = 0,   // kasadan nakit iade
    decimal CardRefundTotal = 0,   // karta iade
    decimal NetCash = 0,           // kasaya net nakit: nakit satış − nakit iade + hediye kartı nakit
    decimal GiftCardSoldTotal = 0, // hediye kartı satışı (EMANET - ciroya dahil değil)
    decimal GiftCardSoldCash = 0,
    decimal GiftCardSoldCard = 0,
    bool CashDetailsHidden = false // true: nakit/ciro alanları yetki nedeniyle gizlendi
);