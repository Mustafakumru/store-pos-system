using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockTransfers.Queries.GetStockTransfers;

public record GetStockTransfersQuery(
    int? StoreId = null,
    TransferStatus? Status = null
) : IRequest<List<StockTransferDto>>;

public record StockTransferDto(
    int Id,
    string TransferNumber,
    string StoreName,
    string Status,
    int TotalProducts,
    int TotalQuantity,
    DateTime CreatedAt,
    DateTime? ShippedAt,
    DateTime? ReceivedAt,
    string? Notes,
    List<StockTransferItemDto> Items
);

public record StockTransferItemDto(
    int VariantId,       // ✅
    int ProductId,
    string ProductName,
    string Barcode,
    string? Color,           // ✅
    string? Size,            // ✅
    int RequestedQuantity,
    int ShippedQuantity,
    int ReceivedQuantity
);

public class GetStockTransfersQueryHandler
    : IRequestHandler<GetStockTransfersQuery, List<StockTransferDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockTransfersQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<StockTransferDto>> Handle(
        GetStockTransfersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.StockTransfers
            .AsNoTracking()
            .Include(t => t.ToStore)
            .Include(t => t.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v.Product)
            .AsQueryable();

        if (request.StoreId.HasValue)
            query = query.Where(t => t.ToStoreId == request.StoreId.Value);

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status.Value);

        var transfers = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return transfers.Select(t => new StockTransferDto(
            Id: t.Id,
            TransferNumber: t.TransferNumber,
            StoreName: t.ToStore.Name,
            Status: t.Status.ToString(),
            TotalProducts: t.Items.Count,
            // Taslakta henüz gönderim yok: talep edilen miktar gösterilir
            // (eskiden taslak talepler listede hep "0 adet" görünüyordu).
            TotalQuantity: t.Status == TransferStatus.Draft
                ? t.Items.Sum(i => i.RequestedQuantity)
                : t.Items.Sum(i => i.ShippedQuantity),
            CreatedAt: t.CreatedAt,
            ShippedAt: t.ShippedAt,
            ReceivedAt: t.ReceivedAt,
            Notes: t.Notes,
            Items: t.Items.Select(i => new StockTransferItemDto(
                i.VariantId,
                i.Variant.ProductId,
                i.Variant.Product.Name,
                i.Variant.Barcode,
                i.Variant.Color,
                i.Variant.Size,
                i.RequestedQuantity,
                i.ShippedQuantity,
                i.ReceivedQuantity
            )).ToList()
        )).ToList();
    }
}