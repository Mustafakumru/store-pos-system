using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockTransfers.Commands.ShipStockTransfer;

public record ShipStockTransferCommand(
    int Id,
    List<ShipItemRequest> Items
) : IRequest;

public record ShipItemRequest(
    int VariantId,        // ✅ ProductId → VariantId
    int ShippedQuantity
);

public class ShipStockTransferCommandHandler
    : IRequestHandler<ShipStockTransferCommand>
{
    private readonly IApplicationDbContext _context;

    public ShipStockTransferCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        ShipStockTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Sevkiyat bulunamadı.");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException(
                "Sadece Draft durumundaki sevkiyatlar gönderilebilir.");

        var variantIds = request.Items.Select(i => i.VariantId).ToList();
        var warehouseStocks = await _context.WarehouseStocks
            .Where(w => variantIds.Contains(w.VariantId))
            .ToDictionaryAsync(w => w.VariantId, cancellationToken);

        foreach (var shipItem in request.Items)
        {
            var transferItem = transfer.Items
                .FirstOrDefault(i => i.VariantId == shipItem.VariantId)
                ?? throw new InvalidOperationException(
                    $"VariantId={shipItem.VariantId} bu sevkiyatta yok.");

            transferItem.ShippedQuantity = shipItem.ShippedQuantity;

            // Depo stoğunu rezerve et
            if (warehouseStocks.TryGetValue(shipItem.VariantId, out var stock))
                stock.ReservedQuantity += shipItem.ShippedQuantity;
        }

        transfer.Status = TransferStatus.Shipped;
        transfer.ShippedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}