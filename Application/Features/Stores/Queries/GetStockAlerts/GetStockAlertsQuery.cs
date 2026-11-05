using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Queries.GetStockAlerts;

public record GetStockAlertsQuery : IRequest<List<StockAlertDto>>;

public record StockAlertDto(
    int StoreId,
    string StoreName,
    string City,
    int VariantId,      // ✅
    int ProductId,
    string ProductName,
    string Barcode,
    string? Color,          // ✅
    string? Size,           // ✅
    int Quantity,
    StockAlertLevel AlertLevel,
    string AlertLevelText
);

public class GetStockAlertsQueryHandler
    : IRequestHandler<GetStockAlertsQuery, List<StockAlertDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockAlertsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<StockAlertDto>> Handle(
        GetStockAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var alerts = await _context.StoreInventories
            .AsNoTracking()
            .Include(si => si.Store)
            .Include(si => si.Variant)
                .ThenInclude(v => v.Product)
            .Where(si =>
                si.Quantity < 0 ||
                si.Quantity == 0 ||
                si.Quantity <= si.MinStockAlert)
            .OrderBy(si => si.Quantity)
            .ToListAsync(cancellationToken);

        return alerts.Select(si => new StockAlertDto(
            StoreId: si.StoreId,
            StoreName: si.Store.Name,
            City: si.Store.City ?? string.Empty,
            VariantId: si.VariantId,
            ProductId: si.Variant.ProductId,
            ProductName: si.Variant.Product.Name,
            Barcode: si.Variant.Barcode,
            Color: si.Variant.Color,
            Size: si.Variant.Size,
            Quantity: si.Quantity,
            AlertLevel: si.AlertLevel,
            AlertLevelText: si.AlertLevel switch
            {
                StockAlertLevel.Critical => "🔴 Negatif Stok",
                StockAlertLevel.OutOfStock => "⛔ Tükendi",
                StockAlertLevel.Low => "🟡 Düşük Stok",
                _ => "🟢 Normal"
            }
        )).ToList();
    }
}