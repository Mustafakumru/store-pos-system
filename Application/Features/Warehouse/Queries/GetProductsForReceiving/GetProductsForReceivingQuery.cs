using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Warehouse.Queries.GetProductsForReceiving;

public record GetProductsForReceivingQuery : IRequest<List<ProductForReceivingDto>>;

public record ProductForReceivingDto(
    int VariantId,
    int ProductId,
    string ProductName,
    string Barcode,
    string? Color,
    string? Size,
    string? ImagePath,
    int CurrentWarehouseStock,
    int ReservedQuantity,
    int AvailableQuantity
);

public class GetProductsForReceivingQueryHandler
    : IRequestHandler<GetProductsForReceivingQuery, List<ProductForReceivingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsForReceivingQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<ProductForReceivingDto>> Handle(
        GetProductsForReceivingQuery request,
        CancellationToken cancellationToken)
    {
        // Tüm aktif varyantları getir
        var variants = await _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => v.IsActive && !v.IsDeleted
                     && v.Product.IsActive && !v.Product.IsDeleted)
            .ToListAsync(cancellationToken);

        // Mevcut depo stoklarını çek
        var warehouseStocks = await _context.WarehouseStocks
            .AsNoTracking()
            .ToDictionaryAsync(w => w.VariantId, cancellationToken);

        return variants.Select(v =>
        {
            warehouseStocks.TryGetValue(v.Id, out var stock);

            return new ProductForReceivingDto(
                VariantId: v.Id,
                ProductId: v.ProductId,
                ProductName: v.Product.Name,
                Barcode: v.Barcode,
                Color: v.Color,
                Size: v.Size,
                ImagePath: v.ImagePath ?? v.Product.ImagePath,
                CurrentWarehouseStock: stock?.Quantity ?? 0,
                ReservedQuantity: stock?.ReservedQuantity ?? 0,
                AvailableQuantity: stock != null
                    ? stock.Quantity - stock.ReservedQuantity
                    : 0
            );
        }).ToList();
    }
}