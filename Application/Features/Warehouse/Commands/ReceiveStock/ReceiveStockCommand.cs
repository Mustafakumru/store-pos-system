using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Warehouse.Commands.ReceiveStock;

public record ReceiveStockCommand(
    List<ReceiveStockItem> Items,
    string? Notes
) : IRequest<ReceiveStockResult>;

public record ReceiveStockItem(
    int VariantId,   // ✅ ProductId → VariantId
    int Quantity
);

public record ReceiveStockResult(
    int TotalProducts,
    int TotalQuantity,
    string Message
);

public class ReceiveStockCommandHandler
    : IRequestHandler<ReceiveStockCommand, ReceiveStockResult>
{
    private readonly IApplicationDbContext _context;

    public ReceiveStockCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ReceiveStockResult> Handle(
        ReceiveStockCommand request,
        CancellationToken cancellationToken)
    {
        var variantIds = request.Items.Select(i => i.VariantId).ToList();

        // Varyantların var olup olmadığını kontrol et
        var variants = await _context.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count != variantIds.Count)
            throw new InvalidOperationException(
                "Listede silinmiş veya bulunamayan varyant var.");

        // Mevcut depo stoklarını çek
        var warehouseStocks = await _context.WarehouseStocks
            .Where(w => variantIds.Contains(w.VariantId))
            .ToDictionaryAsync(w => w.VariantId, cancellationToken);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException(
                    "Miktar 0'dan büyük olmalıdır.");

            if (warehouseStocks.TryGetValue(item.VariantId, out var stock))
            {
                stock.Quantity += item.Quantity;
            }
            else
            {
                _context.WarehouseStocks.Add(new WarehouseStock
                {
                    VariantId = item.VariantId,
                    Quantity = item.Quantity,
                    ReservedQuantity = 0
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ReceiveStockResult(
            TotalProducts: request.Items.Count,
            TotalQuantity: request.Items.Sum(i => i.Quantity),
            Message: "Stok başarıyla depoya eklendi."
        );
    }
}