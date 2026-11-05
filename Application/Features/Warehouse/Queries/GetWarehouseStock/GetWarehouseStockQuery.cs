using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Warehouse.Queries.GetWarehouseStock;

public record GetWarehouseStockQuery : IRequest<List<WarehouseStockDto>>;

public record WarehouseStockDto(
    int VariantId,
    int ProductId,
    string ProductName,
    string Barcode,
    string? Color,
    string? Size,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity
);

public class GetWarehouseStockQueryHandler
    : IRequestHandler<GetWarehouseStockQuery, List<WarehouseStockDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWarehouseStockQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<WarehouseStockDto>> Handle(
        GetWarehouseStockQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.WarehouseStocks
            .AsNoTracking()
            .Include(w => w.Variant)
                .ThenInclude(v => v.Product)
            .Select(w => new WarehouseStockDto(
                w.VariantId,
                w.Variant.ProductId,
                w.Variant.Product.Name,
                w.Variant.Barcode,
                w.Variant.Color,
                w.Variant.Size,
                w.Quantity,
                w.ReservedQuantity,
                w.Quantity - w.ReservedQuantity
            ))
            .ToListAsync(cancellationToken);
    }
}