using MediatR;

namespace Application.Features.Products.Queries.GetAllProducts;

public record GetAllProductsQuery : IRequest<List<ProductListDto>>;

public record ProductListDto(
    int Id,
    string Name,
    string? Description,
    string? ImagePath,
    string CategoryName,
    bool IsActive,
    int VariantCount,
    List<VariantListDto> Variants,
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

public record VariantListDto(
    int Id,
    string Barcode,
    string? Color,
    string? Size,
    string? ImagePath,
    decimal CurrentPrice,
    bool IsActive
);