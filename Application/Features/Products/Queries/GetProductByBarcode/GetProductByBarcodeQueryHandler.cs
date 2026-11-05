using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetProductByBarcode;

public class GetProductByBarcodeQueryHandler
    : IRequestHandler<GetProductByBarcodeQuery, ProductBarcodeDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductByBarcodeQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ProductBarcodeDto> Handle(
        GetProductByBarcodeQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

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

        var price = variant.PriceHistories
            .Where(ph => ph.StartDate <= now
                      && (ph.EndDate == null || ph.EndDate > now))
            .Select(ph => ph.Price)
            .FirstOrDefault();

        return new ProductBarcodeDto
        {
            Id = variant.Product.Id,
            VariantId = variant.Id,
            Barcode = variant.Barcode,
            Name = variant.Product.Name,
            Color = variant.Color,
            Size = variant.Size,
            ImagePath = variant.ImagePath ?? variant.Product.ImagePath,
            CategoryName = variant.Product.Category?.Name ?? "-",
            CategoryId = variant.Product.CategoryId,
            CurrentPrice = price,
            IsActive = variant.IsActive && variant.Product.IsActive
        };
    }
}