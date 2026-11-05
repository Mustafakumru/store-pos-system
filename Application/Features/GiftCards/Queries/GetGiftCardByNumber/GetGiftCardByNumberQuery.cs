using MediatR;

namespace Application.Features.GiftCards.Queries.GetGiftCardByNumber;

/// <summary>
/// WPOS "Hediye Kart Sorgulama" — kart numarasıyla bakiye/durum sorgular.
/// </summary>
public record GetGiftCardByNumberQuery(string CardNumber)
    : IRequest<GiftCardDto>;

public record GiftCardDto(
    string CardNumber,
    decimal Balance,
    DateTime ExpiryDate,
    bool IsActive
);