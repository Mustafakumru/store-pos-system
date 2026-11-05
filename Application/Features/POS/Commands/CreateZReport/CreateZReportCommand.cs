using MediatR;

namespace Application.Features.POS.Commands.CreateZReport;

/// <summary>
/// WPOS "Z Raporu" — son Z raporundan bu yana özet alınır, kalıcı olarak
/// kaydedilir ve mağaza bazında artan bir Z numarası atanır (sayaç kapanışı).
/// </summary>
/// <param name="ApprovalToken">Kasiyer alıyorsa müdür onayı ("z-report") zorunlu.</param>
/// <param name="IsManager">İsteği yapan StoreManager/Admin ise onay gerekmez (WebAPI rolden doldurur).</param>
public record CreateZReportCommand(string? ApprovalToken = null, bool IsManager = false) : IRequest<ZReportDto>;

public record ZReportDto(
    int Id,
    int StoreId,
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
    decimal CashRefundTotal = 0,
    decimal CardRefundTotal = 0,
    decimal GiftCardSoldTotal = 0,
    decimal GiftCardSoldCash = 0,
    decimal GiftCardSoldCard = 0
);