using Domain.Enums;
using MediatR;

namespace Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Yeni urun (model). Urun kodu SUNUCUDA uretilir: Sezon + yil + kategori +
/// model sira no + yas kitlesi (bolumden turetilir) -> orn. W6ER01Z8.
/// Varyantlarda barkod bos birakilirsa EAN-13 uretilir.
/// </summary>
public record CreateProductCommand(
    string Name,
    string? Description,
    int CategoryId,
    Season Season,
    int SeasonYear,
    Department Department,
    string? ImagePath,
    List<CreateVariantRequest> Variants
) : IRequest<CreateProductResult>;

public record CreateVariantRequest(
    string? Barcode,          // bos = sunucu uretir
    string? Color,
    string? Size,
    string? ImagePath,
    decimal InitialPrice
);

public record CreateProductResult(int Id, string Code, List<CreatedVariantDto> Variants);

public record CreatedVariantDto(int Id, string Barcode, string? Color, string? Size);
