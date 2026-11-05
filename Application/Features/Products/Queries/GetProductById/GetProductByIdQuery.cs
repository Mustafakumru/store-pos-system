using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetProductById;

/// <summary>
/// Admin panelindeki ürün düzenleme ekranı için ürünün TAMAMINI döner:
/// açıklama, kategori, aktiflik ve tüm (aktif + pasif) varyantlar.
/// (Eskiden ilk aktif varyantı ProductBarcodeDto olarak dönüyordu; açıklama
/// ve varyant listesi olmadığı için düzenleme ekranı açıklamayı siliyor,
/// pasif ürünü aktife alıyor ve aktif varyantı olmayan ürünü açamıyordu.)
/// </summary>
public record GetProductByIdQuery(int Id) : IRequest<ProductDetailDto>;

public record ProductDetailDto(
    int Id,
    string Name,
    string? Description,
    string? ImagePath,
    int CategoryId,
    string CategoryName,
    bool IsActive,
    int VariantCount,
    List<ProductDetailVariantDto> Variants,
    // Urun kodu ve siniflandirma (eski kayitlarda geriye donuk doldurulur)
    string? Code = null,
    int? Season = null,
    string? SeasonLabel = null,
    int? SeasonYear = null,
    int? Department = null,
    string? DepartmentLabel = null,
    int? AgeGroup = null,
    string? AgeGroupLabel = null
);

public record ProductDetailVariantDto(
    int Id,
    string Barcode,
    string? Color,
    string? Size,
    string? ImagePath,
    decimal CurrentPrice,
    bool IsActive
);

public class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ProductDetailDto> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.PriceHistories)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ID={request.Id} ürün bulunamadı.");

        var variants = product.Variants
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.Color).ThenBy(v => v.Size)
            .Select(v => new ProductDetailVariantDto(
                v.Id,
                v.Barcode,
                v.Color,
                v.Size,
                v.ImagePath,
                v.PriceHistories
                    .Where(ph => ph.StartDate <= now && (ph.EndDate == null || ph.EndDate > now))
                    .Select(ph => ph.Price)
                    .FirstOrDefault(),
                v.IsActive))
            .ToList();

        return new ProductDetailDto(
            product.Id,
            product.Name,
            product.Description,
            product.ImagePath,
            product.CategoryId,
            product.Category?.Name ?? "-",
            product.IsActive,
            variants.Count,
            variants,
            product.Code,
            (int?)product.Season,
            product.Season is { } s ? Domain.Common.ProductCode.SeasonLabel(s) : null,
            product.SeasonYear,
            (int?)product.Department,
            product.Department is { } d ? Domain.Common.ProductCode.DepartmentLabel(d) : null,
            (int?)product.AgeGroup,
            product.AgeGroup is { } a ? Domain.Common.ProductCode.AgeGroupLabel(a) : null);
    }
}
