using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.GiftCards.Queries.GetGiftCardByNumber;

public class GetGiftCardByNumberQueryHandler
    : IRequestHandler<GetGiftCardByNumberQuery, GiftCardDto>
{
    private readonly IApplicationDbContext _context;

    public GetGiftCardByNumberQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<GiftCardDto> Handle(
        GetGiftCardByNumberQuery request,
        CancellationToken cancellationToken)
    {
        var card = await _context.GiftCards
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.CardNumber == request.CardNumber, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"'{request.CardNumber}' numaralı hediye kartı bulunamadı.");

        return new GiftCardDto(
            card.CardNumber, card.Balance, card.ExpiryDate, card.IsActive);
    }
}