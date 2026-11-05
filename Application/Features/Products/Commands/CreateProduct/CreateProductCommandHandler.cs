using Application.Common.Interfaces;
using Application.Features.Products.Common;
using Domain.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.CreateProduct;

public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IApplicationDbContext _context;

    public CreateProductCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
            throw new InvalidOperationException("Urun adi en az 2 karakter olmalidir.");
        ProductCatalog.ValidateClassification(request.Season, request.SeasonYear, request.Department);

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Kategori bulunamadi.");

        var catalog = new ProductCatalog(_context);
        var code = await catalog.GenerateProductCodeAsync(
            category, request.Season, request.SeasonYear, request.Department, cancellationToken);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            CategoryId = category.Id,
            Category = category,
            ImagePath = request.ImagePath,
            IsActive = true,
            Code = code,
            Season = request.Season,
            SeasonYear = request.SeasonYear,
            Department = request.Department,
            AgeGroup = ProductCode.AgeGroupOf(request.Department)
        };
        _context.Products.Add(product);

        foreach (var v in request.Variants ?? new())
            await catalog.CreateVariantAsync(product, v.Barcode, v.Color, v.Size,
                v.ImagePath, v.InitialPrice, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id, code,
            product.Variants.Select(v => new CreatedVariantDto(v.Id, v.Barcode, v.Color, v.Size)).ToList());
    }
}
