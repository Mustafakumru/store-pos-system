using MediatR;

namespace Application.Features.POS.Queries.GetLastSale;

/// <summary>
/// WPOS "Son Belge Tekrarı" — kasiyerin en son tamamlanan satışını döner.
/// Reprint öncesi mevcut fişi göstermek için kullanılır.
/// </summary>
public record GetLastSaleQuery : IRequest<LastSaleDto?>;

public record LastSaleDto(
    int Id,
    string ReceiptNumber,
    decimal FinalAmount,
    DateTime SaleDate,
    string Status
);