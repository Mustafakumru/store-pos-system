using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Queries.GetStoreInventory;

public record GetStoreInventoryQuery(
    int StoreId,
    bool? OnlyAlerts = null
) : IRequest<StoreInventoryResult>;

public record StoreInventoryResult(
    int StoreId,
    string StoreName,
    int TotalProducts,
    int CriticalCount,
    int OutOfStockCount,
    int LowStockCount,
    List<StoreInventoryItem> Items
);

public record StoreInventoryItem(
    int VariantId,      // ✅
    int ProductId,
    string ProductName,
    string Barcode,
    string? Color,          // ✅
    string? Size,           // ✅
    string? ImagePath,
    int Quantity,
    int MinStockAlert,
    StockAlertLevel AlertLevel,
    string AlertLevelText,
    decimal CurrentPrice
);

public class GetStoreInventoryQueryHandler
    : IRequestHandler<GetStoreInventoryQuery, StoreInventoryResult>
{
    private readonly IApplicationDbContext _context;

    public GetStoreInventoryQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<StoreInventoryResult> Handle(
        GetStoreInventoryQuery request,
        CancellationToken cancellationToken)
    {
        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StoreId, cancellationToken)
            ?? throw new KeyNotFoundException("Mağaza bulunamadı.");

        var inventories = await _context.StoreInventories
            .AsNoTracking()
            .Include(si => si.Variant)
                .ThenInclude(v => v.Product)
            .Include(si => si.Variant)
                .ThenInclude(v => v.PriceHistories)
            .Where(si => si.StoreId == request.StoreId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var items = inventories
            .Where(i => request.OnlyAlerts != true
                     || i.AlertLevel != StockAlertLevel.Normal)
            .Select(i =>
            {
                var price = i.Variant.PriceHistories
                    .Where(ph => ph.StartDate <= now
                              && (ph.EndDate == null || ph.EndDate > now))
                    .OrderByDescending(ph => ph.StartDate)   // çakışan kayıtta en güncel fiyat
                    .Select(ph => ph.Price)
                    .FirstOrDefault();

                return new StoreInventoryItem(
                    VariantId: i.VariantId,
                    ProductId: i.Variant.ProductId,
                    ProductName: i.Variant.Product.Name,
                    Barcode: i.Variant.Barcode,
                    Color: i.Variant.Color,
                    Size: i.Variant.Size,
                    ImagePath: i.Variant.ImagePath ?? i.Variant.Product.ImagePath,
                    Quantity: i.Quantity,
                    MinStockAlert: i.MinStockAlert,
                    AlertLevel: i.AlertLevel,
                    AlertLevelText: i.AlertLevel switch
                    {
                        StockAlertLevel.Critical => "🔴 Negatif Stok",
                        StockAlertLevel.OutOfStock => "⛔ Tükendi",
                        StockAlertLevel.Low => "🟡 Düşük Stok",
                        _ => "🟢 Normal"
                    },
                    CurrentPrice: price
                );
            })
            // En kötü durum önce: Critical(3) → OutOfStock(2) → Low(1) → Normal(0).
            // Eskiden artan sıralıydı; dashboard'daki "ilk 5 uyarı" negatif
            // stokları değil düşük stokları gösteriyordu.
            .OrderByDescending(i => i.AlertLevel)
            .ThenBy(i => i.ProductName)
            .ToList();

        return new StoreInventoryResult(
            StoreId: store.Id,
            StoreName: store.Name,
            TotalProducts: items.Count,
            CriticalCount: items.Count(i => i.AlertLevel == StockAlertLevel.Critical),
            OutOfStockCount: items.Count(i => i.AlertLevel == StockAlertLevel.OutOfStock),
            LowStockCount: items.Count(i => i.AlertLevel == StockAlertLevel.Low),
            Items: items
        );
    }
}