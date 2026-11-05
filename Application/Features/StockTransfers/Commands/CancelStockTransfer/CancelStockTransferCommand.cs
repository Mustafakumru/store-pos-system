using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockTransfers.Commands.CancelStockTransfer;

public record CancelStockTransferCommand(
    int Id,
    string? Reason,
    int? ActingStoreId = null   // Mağaza müdürü: sadece kendi mağazasının TASLAK talebini iptal edebilir
) : IRequest;

public class CancelStockTransferCommandHandler
    : IRequestHandler<CancelStockTransferCommand>
{
    private readonly IApplicationDbContext _context;

    public CancelStockTransferCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        CancelStockTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Sevkiyat bulunamadı.");

        if (request.ActingStoreId.HasValue)
        {
            if (transfer.ToStoreId != request.ActingStoreId.Value)
                throw new Application.Common.Exceptions.ForbiddenAccessException(
                    "Sadece mağazanızın sevkiyat taleplerini iptal edebilirsiniz.");
            if (transfer.Status != TransferStatus.Draft)
                throw new InvalidOperationException(
                    "Yola çıkmış sevkiyatı yalnızca merkez (Admin) iptal edebilir.");
        }

        if (transfer.Status == TransferStatus.Received)
            throw new InvalidOperationException(
                "Teslim alınmış sevkiyat iptal edilemez.");

        if (transfer.Status == TransferStatus.Cancelled)
            throw new InvalidOperationException(
                "Bu sevkiyat zaten iptal edilmiş.");

        // Shipped ise rezervasyonları geri al
        if (transfer.Status == TransferStatus.Shipped)
        {
            var variantIds = transfer.Items.Select(i => i.VariantId).ToList();
            var warehouseStocks = await _context.WarehouseStocks
                .Where(w => variantIds.Contains(w.VariantId))
                .ToDictionaryAsync(w => w.VariantId, cancellationToken);

            foreach (var item in transfer.Items)
            {
                if (warehouseStocks.TryGetValue(item.VariantId, out var stock))
                {
                    stock.ReservedQuantity -= item.ShippedQuantity;
                    if (stock.ReservedQuantity < 0)
                        stock.ReservedQuantity = 0;
                }
            }
        }

        transfer.Status = TransferStatus.Cancelled;
        transfer.Notes = string.IsNullOrEmpty(request.Reason)
            ? transfer.Notes
            : $"{transfer.Notes} | İptal: {request.Reason}".Trim('|', ' ');

        await _context.SaveChangesAsync(cancellationToken);
    }
}