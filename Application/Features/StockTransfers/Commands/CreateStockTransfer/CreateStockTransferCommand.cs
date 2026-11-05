using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockTransfers.Commands.CreateStockTransfer;

public record CreateStockTransferCommand(
    int ToStoreId,
    List<TransferItemRequest> Items,
    string? Notes
) : IRequest<CreateStockTransferResult>;

public record TransferItemRequest(
    int VariantId,          // ✅ ProductId → VariantId
    int RequestedQuantity
);

public record CreateStockTransferResult(
    int Id,
    string TransferNumber,
    string StoreName
);

public class CreateStockTransferCommandHandler
    : IRequestHandler<CreateStockTransferCommand, CreateStockTransferResult>
{
    private readonly IApplicationDbContext _context;

    public CreateStockTransferCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CreateStockTransferResult> Handle(
        CreateStockTransferCommand request,
        CancellationToken cancellationToken)
    {
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.Id == request.ToStoreId, cancellationToken)
            ?? throw new KeyNotFoundException("Mağaza bulunamadı.");

        if (!store.IsActive)
            throw new InvalidOperationException(
                "Pasif mağazaya sevkiyat yapılamaz.");

        // Varyantlar katalogda var mı?
        var variantIds = request.Items.Select(i => i.VariantId).ToList();
        var variants = await _context.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count != variantIds.Count)
            throw new InvalidOperationException(
                "Listede bulunamayan veya silinmiş varyant var.");

        // Miktar kontrolü
        foreach (var item in request.Items)
            if (item.RequestedQuantity <= 0)
                throw new InvalidOperationException(
                    "Sevkiyat miktarı 0'dan büyük olmalıdır.");

        var transferNumber = await GenerateTransferNumberAsync(cancellationToken);

        var transfer = new StockTransfer
        {
            TransferNumber = transferNumber,
            ToStoreId = request.ToStoreId,
            Status = TransferStatus.Draft,
            Notes = request.Notes,
            Items = request.Items.Select(i => new StockTransferItem
            {
                VariantId = i.VariantId,  // ✅
                RequestedQuantity = i.RequestedQuantity,
                ShippedQuantity = 0,
                ReceivedQuantity = 0
            }).ToList()
        };

        _context.StockTransfers.Add(transfer);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateStockTransferResult(
            transfer.Id,
            transfer.TransferNumber,
            store.Name);
    }

    private async Task<string> GenerateTransferNumberAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"TRF-{today}-";
        var count = await _context.StockTransfers
            .CountAsync(t => t.TransferNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{(count + 1):D4}";
    }
}