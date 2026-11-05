using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.ProductLookup;

// ════════════════════════════════════════════════════════════════
// Barkodsuz urun bulma (Fiyat Gor / Fis ekrani / mobil uygulama):
//   1) Kasiyer urun kodunu yazar (W6ER01Z8)  -> GetProductByCodeQuery
//      Kod tam degilse -> SearchProductsQuery (kod/ad parcasiyla liste)
//   2) Renk secer -> o rengin bedenleri (fiyat + magaza stogu) gelir
//   3) Beden secer -> VariantId/Barkod ile sepete eklenir (mevcut akis)
// ════════════════════════════════════════════════════════════════

public record GetProductByCodeQuery(string Code, int StoreId) : IRequest<ProductByCodeDto>;

public record ProductByCodeDto(
    int ProductId,
    string Code,
    string Name,
    string CategoryName,
    string? ImagePath,
    string? SeasonLabel,      // "Kis 2026"
    string? DepartmentLabel,  // "Erkek"
    string? AgeGroupLabel,    // "Yetiskin (Z8)"
    decimal MinPrice,
    decimal MaxPrice,
    int TotalStock,
    List<ProductColorOptionDto> Colors);

public record ProductColorOptionDto(
    string Color,
    string? ImagePath,
    int TotalStock,
    List<ProductSizeOptionDto> Sizes);

public record ProductSizeOptionDto(
    int VariantId,
    string? Size,
    string Barcode,
    decimal Price,
    int StockQuantity,
    bool IsSellable);        // aktif + fiyati var

public class GetProductByCodeQueryHandler : IRequestHandler<GetProductByCodeQuery, ProductByCodeDto>
{
    private readonly IApplicationDbContext _context;
    public GetProductByCodeQueryHandler(IApplicationDbContext context) => _context = context;

    // Beden siralamasi: harf bedenler mantiksal sirada, sayilar sayisal
    private static readonly string[] LetterSizes = { "XXS", "XS", "S", "M", "L", "XL", "XXL", "2XL", "3XL", "4XL" };

    internal static (int, decimal, string) SizeSortKey(string? size)
    {
        var s = (size ?? string.Empty).ToUpperInvariant();
        var idx = Array.IndexOf(LetterSizes, s);
        if (idx >= 0) return (0, idx, s);
        var numPart = new string(s.TakeWhile(c => char.IsDigit(c) || c == ',' || c == '.').ToArray()).Replace(',', '.');
        if (decimal.TryParse(numPart, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var n)) return (1, n, s);
        return (2, 0, s);
    }

    public async Task<ProductByCodeDto> Handle(GetProductByCodeQuery request, CancellationToken ct)
    {
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (ProductCode.TryParse(code) is null)
            throw new InvalidOperationException(
                $"'{request.Code}' gecerli bir urun kodu degil. Ornek: W6ER01Z8 (8 karakter).");

        var now = DateTime.UtcNow;
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants).ThenInclude(v => v.PriceHistories)
            .Include(p => p.Variants).ThenInclude(v => v.Group!).ThenInclude(g => g.Images)
            .FirstOrDefaultAsync(p => p.Code == code, ct)
            ?? throw new KeyNotFoundException($"'{code}' kodlu urun bulunamadi.");

        if (!product.IsActive)
            throw new InvalidOperationException("Bu urun satisa kapali.");

        var variantIds = product.Variants.Select(v => v.Id).ToList();
        var stocks = await _context.StoreInventories.AsNoTracking()
            .Where(si => si.StoreId == request.StoreId && variantIds.Contains(si.VariantId))
            .ToDictionaryAsync(si => si.VariantId, si => si.Quantity, ct);

        var sizes = product.Variants
            .Where(v => !v.IsDeleted)
            .Select(v =>
            {
                var price = v.PriceHistories
                    .Where(ph => !ph.IsDeleted && ph.StartDate <= now && (ph.EndDate == null || ph.EndDate > now))
                    .OrderByDescending(ph => ph.StartDate)
                    .Select(ph => ph.Price).FirstOrDefault();
                return new
                {
                    Variant = v,
                    Option = new ProductSizeOptionDto(v.Id, v.Size, v.Barcode, price,
                        stocks.GetValueOrDefault(v.Id), v.IsActive && price > 0)
                };
            })
            .ToList();

        var colors = sizes
            .GroupBy(x => x.Variant.Color ?? "Standart")
            .Select(g =>
            {
                var groupImage = g.Select(x => x.Variant.Group).FirstOrDefault(gr => gr != null)?
                    .Images.Where(i => !i.IsDeleted).OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault();
                var image = groupImage
                            ?? g.Select(x => x.Variant.ImagePath).FirstOrDefault(p => p != null)
                            ?? product.ImagePath;
                var opts = g.Select(x => x.Option).OrderBy(o => SizeSortKey(o.Size)).ToList();
                return new ProductColorOptionDto(g.Key, image, opts.Sum(o => o.StockQuantity), opts);
            })
            .OrderBy(c => c.Color)
            .ToList();

        var sellablePrices = sizes.Where(s => s.Option.Price > 0).Select(s => s.Option.Price).ToList();

        return new ProductByCodeDto(
            product.Id,
            code,
            product.Name,
            product.Category?.Name ?? "-",
            product.ImagePath,
            product.Season is { } s ? $"{ProductCode.SeasonLabel(s)} {product.SeasonYear}" : null,
            product.Department is { } d ? ProductCode.DepartmentLabel(d) : null,
            product.AgeGroup is { } a ? ProductCode.AgeGroupLabel(a) : null,
            sellablePrices.Count > 0 ? sellablePrices.Min() : 0,
            sellablePrices.Count > 0 ? sellablePrices.Max() : 0,
            colors.Sum(c => c.TotalStock),
            colors);
    }
}

// ── Parca kod / ad ile arama (en fazla 20 sonuc) ─────────────────────

public record SearchProductsQuery(string Term) : IRequest<List<ProductSearchItemDto>>;

public record ProductSearchItemDto(int ProductId, string Code, string Name, string CategoryName, string? ImagePath, int VariantCount);

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, List<ProductSearchItemDto>>
{
    private readonly IApplicationDbContext _context;
    public SearchProductsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ProductSearchItemDto>> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        var term = (request.Term ?? string.Empty).Trim();
        if (term.Length < 2) return new();
        var upper = term.ToUpperInvariant();

        return await _context.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Code != null
                        && (p.Code!.StartsWith(upper) || p.Name.Contains(term)))
            .OrderBy(p => p.Code!.StartsWith(upper) ? 0 : 1).ThenBy(p => p.Code)
            .Take(20)
            .Select(p => new ProductSearchItemDto(p.Id, p.Code!, p.Name, p.Category.Name, p.ImagePath,
                p.Variants.Count(v => !v.IsDeleted && v.IsActive)))
            .ToListAsync(ct);
    }
}
