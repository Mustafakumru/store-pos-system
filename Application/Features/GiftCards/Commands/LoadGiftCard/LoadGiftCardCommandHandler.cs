using Application.Common.Interfaces;
using Application.Features.GiftCards.Queries.GetGiftCardByNumber;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.GiftCards.Commands.LoadGiftCard;

public class LoadGiftCardCommandHandler
    : IRequestHandler<LoadGiftCardCommand, GiftCardDto>
{
    private readonly IApplicationDbContext _context;

    // WPOS örneklerinde "01.01.2100" = pratikte süresiz kart anlamına gelir
    private static readonly DateTime DefaultExpiryDate = new(2100, 1, 1);

    public LoadGiftCardCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<GiftCardDto> Handle(
        LoadGiftCardCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new InvalidOperationException(
                "Yükleme tutarı sıfırdan büyük olmalıdır.");

        var card = await _context.GiftCards
            .FirstOrDefaultAsync(
                g => g.CardNumber == request.CardNumber, cancellationToken);

        if (card is null)
        {
            // Boş kart okutulup ilk yükleme yapılıyor → yeni kart oluştur
            card = new GiftCard
            {
                CardNumber = request.CardNumber,
                Balance = 0,
                ExpiryDate = DefaultExpiryDate,
                IsActive = true
            };
            _context.GiftCards.Add(card);
        }
        else if (!card.IsActive)
        {
            throw new InvalidOperationException("Bu hediye kartı pasif durumda.");
        }

        card.Balance += request.Amount;

        _context.GiftCardTransactions.Add(new GiftCardTransaction
        {
            GiftCard = card,
            Type = GiftCardTransactionType.Load,
            Amount = request.Amount,
            BalanceAfter = card.Balance,
            SaleId = request.SaleId
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new GiftCardDto(
            card.CardNumber, card.Balance, card.ExpiryDate, card.IsActive);
    }
}