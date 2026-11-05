using MediatR;

namespace Application.Features.Products.Queries.GetProductByBarcode;

public record GetProductByBarcodeQuery(string Barcode)
    : IRequest<ProductBarcodeDto>;

public record ProductBarcodeDto
{
    public int Id { get; init; }
    public int VariantId { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? Size { get; init; }
    public string? ImagePath { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public decimal CurrentPrice { get; init; }
    public bool IsActive { get; init; }
}