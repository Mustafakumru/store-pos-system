using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetSuspendedCarts;

public class GetSuspendedCartsQueryHandler
    : IRequestHandler<GetSuspendedCartsQuery, List<SuspendedCartDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSuspendedCartsQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SuspendedCartDto>> Handle(
        GetSuspendedCartsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        return await _context.SuspendedSales
            .AsNoTracking()
            .Where(s => s.StoreId == storeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SuspendedCartDto(s.Id, s.CashierId, s.Note, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}