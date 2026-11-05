using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Queries.GetStores;

public record GetStoresQuery : IRequest<List<StoreDto>>;

public record StoreDto(
    int Id,
    string Name,
    string Code,
    string? City,
    string? Address,
    string? Phone,
    bool IsActive,

    // Stok uyarısı olan ürün sayısı
    int CriticalStockCount,
    int LowStockCount
);

public class GetStoresQueryHandler
    : IRequestHandler<GetStoresQuery, List<StoreDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStoresQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<StoreDto>> Handle(
        GetStoresQuery request,
        CancellationToken cancellationToken)
    {
        var stores = await _context.Stores
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Tüm stok uyarılarını tek sorguda çek
        var inventories = await _context.StoreInventories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return stores.Select(s =>
        {
            var storeInventory = inventories
                .Where(i => i.StoreId == s.Id)
                .ToList();

            return new StoreDto(
                Id: s.Id,
                Name: s.Name,
                Code: s.Code,
                City: s.City,
                Address: s.Address,
                Phone: s.Phone,
                IsActive: s.IsActive,
                CriticalStockCount: storeInventory.Count(i => i.Quantity < 0),
                LowStockCount: storeInventory.Count(
                    i => i.Quantity >= 0 && i.Quantity <= i.MinStockAlert)
            );
        }).ToList();
    }
}