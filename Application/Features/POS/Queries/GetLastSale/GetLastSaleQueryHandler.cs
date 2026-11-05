using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetLastSale;

public class GetLastSaleQueryHandler
    : IRequestHandler<GetLastSaleQuery, LastSaleDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetLastSaleQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LastSaleDto?> Handle(
        GetLastSaleQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        return await _context.Sales
            .AsNoTracking()
            .Where(s => s.StoreId == storeId
                     && s.CashierId == _currentUser.UserId
                     && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new LastSaleDto(
                s.Id,
                s.ReceiptNumber,
                s.FinalAmount,
                s.CreatedAt,
                s.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
