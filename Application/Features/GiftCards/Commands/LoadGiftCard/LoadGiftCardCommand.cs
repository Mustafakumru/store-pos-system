using Application.Features.GiftCards.Queries.GetGiftCardByNumber;
using MediatR;

namespace Application.Features.GiftCards.Commands.LoadGiftCard;

/// <summary>
/// WPOS "Hediye Kart Yükleme" — karta bakiye yükler.
/// Kart sistemde kayıtlı değilse, yeni kart olarak oluşturulur
/// (boş kart okutup ilk yükleme senaryosu).
/// </summary>
public record LoadGiftCardCommand(
    string CardNumber,
    decimal Amount,
    int? SaleId = null
) : IRequest<GiftCardDto>, Application.Common.Behaviors.IConcurrencySensitiveCommand;