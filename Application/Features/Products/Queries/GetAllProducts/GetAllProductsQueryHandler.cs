using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetAllProducts;

public class GetAllProductsQueryHandler
    : IRequestHandler<GetAllProductsQuery, List<ProductListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllProductsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<ProductListDto>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.PriceHistories)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        return products.Select(p => new ProductListDto(
            Id: p.Id,
            Name: p.Name,
            Description: p.Description,
            ImagePath: p.ImagePath,
            CategoryName: p.Category?.Name ?? "-",
            IsActive: p.IsActive,
            VariantCount: p.Variants.Count(v => !v.IsDeleted),
            Variants: p.Variants
                .Where(v => !v.IsDeleted)
                .Select(v => new VariantListDto(
                    Id: v.Id,
                    Barcode: v.Barcode,
                    Color: v.Color,
                    Size: v.Size,
                    ImagePath: v.ImagePath ?? p.ImagePath,
                    CurrentPrice: v.PriceHistories
                        .Where(ph => ph.StartDate <= now
                                  && (ph.EndDate == null || ph.EndDate > now))
                        .Select(ph => ph.Price)
                        .FirstOrDefault(),
                    IsActive: v.IsActive
                )).ToList(),
            Code: p.Code,
            Season: (int?)p.Season,
            SeasonLabel: p.Season is { } s ? Domain.Common.ProductCode.SeasonLabel(s) : null,
            SeasonYear: p.SeasonYear,
            Department: (int?)p.Department,
            DepartmentLabel: p.Department is { } d ? Domain.Common.ProductCode.DepartmentLabel(d) : null,
            AgeGroup: (int?)p.AgeGroup,
            AgeGroupLabel: p.AgeGroup is { } a ? Domain.Common.ProductCode.AgeGroupLabel(a) : null
        )).ToList();
    }
}