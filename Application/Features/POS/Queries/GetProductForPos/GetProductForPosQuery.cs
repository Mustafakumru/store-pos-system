using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetProductForPos;

public record GetProductForPosQuery(
    string Barcode,
    int StoreId
) : IRequest<ProductForPosDto>;

public record ProductForPosDto(
    int ProductId,
    int VariantId,       // ✅ Yeni
    string Barcode,
    string Name,
    string? Color,           // ✅ Yeni
    string? Size,            // ✅ Yeni
    string? ImagePath,
    string CategoryName,
    decimal CurrentPrice,
    int StockQuantity,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsNegativeStock,
    string? ProductCode = null    // urun (model) kodu: W6ER01Z8
);

public class GetProductForPosQueryHandler
    : IRequestHandler<GetProductForPosQuery, ProductForPosDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductForPosQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ProductForPosDto> Handle(
        GetProductForPosQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // ── Barkod → Varyant ────────────────────────────────
        var variant = await _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p.Category)
            .Include(v => v.PriceHistories)
            .FirstOrDefaultAsync(
                v => v.Barcode == request.Barcode,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"'{request.Barcode}' barkodlu ürün bulunamadı.");

        if (!variant.IsActive || !variant.Product.IsActive)
            throw new InvalidOperationException(
                "Bu ürün satışa kapalı.");

        // ── Aktif fiyat ─────────────────────────────────────
        var price = variant.PriceHistories
            .Where(ph => ph.StartDate <= now
                      && (ph.EndDate == null || ph.EndDate > now))
            .Select(ph => ph.Price)
            .FirstOrDefault();

        if (price == 0)
            throw new InvalidOperationException(
                "Bu ürün için aktif fiyat tanımlanmamış.");

        // ── Mağaza stoğu ─────────────────────────────────────
        var inventory = await _context.StoreInventories
            .AsNoTracking()
            .Where(si => si.StoreId == request.StoreId
                      && si.VariantId == variant.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var stock = inventory?.Quantity ?? 0;
        var minAlert = inventory?.MinStockAlert ?? 5;

        return new ProductForPosDto(
            ProductId: variant.ProductId,
            VariantId: variant.Id,
            Barcode: variant.Barcode,
            Name: variant.Product.Name,
            Color: variant.Color,
            Size: variant.Size,
            ImagePath: variant.ImagePath ?? variant.Product.ImagePath,
            CategoryName: variant.Product.Category?.Name ?? "-",
            CurrentPrice: price,
            StockQuantity: stock,
            IsLowStock: stock > 0 && stock <= minAlert,
            IsOutOfStock: stock == 0,
            IsNegativeStock: stock < 0,
            ProductCode: variant.Product.Code
        );
    }
}