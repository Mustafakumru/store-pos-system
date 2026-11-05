using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.OfflineCatalog;

/// <summary>
/// Kasanın çevrimdışı satış için yerel diske indirdiği katalog: satılabilir her
/// varyantın barkodu, ürün kodu, adı, rengi, bedeni ve GÜNCEL fiyatı.
/// Stok bilgisi yoktur (çevrimdışıyken stok güncel olamaz; satış eksiye düşebilir).
/// </summary>
public record GetOfflineCatalogQuery : IRequest<OfflineCatalogDto>;

public record OfflineCatalogDto(DateTime GeneratedAtUtc, List<OfflineCatalogItemDto> Items);

public record OfflineCatalogItemDto(
    int VariantId, int ProductId, string Barcode, string? ProductCode, string Name,
    string? Color, string? Size, string CategoryName, decimal Price, string? ImagePath);

public class GetOfflineCatalogQueryHandler : IRequestHandler<GetOfflineCatalogQuery, OfflineCatalogDto>
{
    private readonly IApplicationDbContext _db;
    public GetOfflineCatalogQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<OfflineCatalogDto> Handle(GetOfflineCatalogQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.ProductVariants.AsNoTracking()
            .Where(v => !v.IsDeleted && v.IsActive && !v.Product.IsDeleted && v.Product.IsActive)
            .Select(v => new
            {
                v.Id, v.ProductId, v.Barcode, v.Product.Code, v.Product.Name, v.Color, v.Size,
                Category = v.Product.Category.Name,
                Image = v.ImagePath ?? v.Product.ImagePath,
                Price = v.PriceHistories
                    .Where(ph => ph.StartDate <= now && (ph.EndDate == null || ph.EndDate > now))
                    .OrderByDescending(ph => ph.StartDate)
                    .Select(ph => (decimal?)ph.Price)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows
            .Where(r => r.Price is > 0 && !string.IsNullOrWhiteSpace(r.Barcode))
            .Select(r => new OfflineCatalogItemDto(r.Id, r.ProductId, r.Barcode, r.Code, r.Name,
                r.Color, r.Size, r.Category, r.Price!.Value, r.Image))
            .ToList();
        return new OfflineCatalogDto(now, items);
    }
}
