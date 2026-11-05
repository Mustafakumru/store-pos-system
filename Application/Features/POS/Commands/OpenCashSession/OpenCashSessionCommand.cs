using MediatR;

namespace Application.Features.POS.Commands.OpenCashSession;

/// <summary>
/// WPOS "Avans" — gün başı kasa açılışı. Yönetici onayı gerektirir.
/// Kasiyerin zaten açık bir oturumu varsa hata döner.
/// </summary>
public record OpenCashSessionCommand(
    decimal OpeningFloat,
    string ApprovalToken
) : IRequest<CashSessionDto>;

public record CashSessionDto(
    int Id,
    int StoreId,
    string CashierId,
    decimal OpeningFloat,
    DateTime OpenedAt,
    decimal? ClosingAmount,
    DateTime? ClosedAt,
    string Status
);