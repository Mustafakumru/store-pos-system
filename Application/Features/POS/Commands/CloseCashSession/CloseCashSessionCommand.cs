using Application.Features.POS.Commands.OpenCashSession;
using MediatR;

namespace Application.Features.POS.Commands.CloseCashSession;

/// <summary>
/// WPOS "Teslim" — gün sonu kasa kapanışı. Yönetici onayı gerektirir.
/// Kasiyerin açık oturumunu kapatır.
/// </summary>
public record CloseCashSessionCommand(
    decimal ClosingAmount,
    string ApprovalToken
) : IRequest<CashSessionDto>;