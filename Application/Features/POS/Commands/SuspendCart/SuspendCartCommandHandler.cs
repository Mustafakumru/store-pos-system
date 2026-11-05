using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.POS.Commands.SuspendCart;

public class SuspendCartCommandHandler
    : IRequestHandler<SuspendCartCommand, SuspendCartResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SuspendCartCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SuspendCartResult> Handle(
        SuspendCartCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        if (string.IsNullOrWhiteSpace(request.CartDataJson))
            throw new InvalidOperationException("Sepet içeriği boş olamaz.");

        var suspended = new SuspendedSale
        {
            StoreId = int.Parse(_currentUser.StoreId),
            CashierId = _currentUser.UserId,
            CartDataJson = request.CartDataJson,
            Note = request.Note
        };

        _context.SuspendedSales.Add(suspended);
        await _context.SaveChangesAsync(cancellationToken);

        return new SuspendCartResult(suspended.Id, suspended.CreatedAt);
    }
}