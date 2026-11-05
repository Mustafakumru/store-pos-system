using Application.Common.Interfaces;
using Application.Features.Products.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.GenerateVariants;

/// <summary>
/// Renk x Beden matrisi: urunde OLMAYAN tum kombinasyonlari olusturur,
/// barkodlarini uretir, renk gruplarina baglar. Var olanlar atlanir.
/// Fiyat: renk bazli (ColorPrices) varsa o, yoksa varsayilan (Price).
/// </summary>
public record GenerateVariantsCommand(
    int ProductId,
    List<string> Colors,
    List<string> Sizes,
    decimal Price,
    Dictionary<string, decimal>? ColorPrices = null
) : IRequest<GenerateVariantsResult>;

public record GenerateVariantsResult(int Created, int Skipped, List<GeneratedVariantDto> Variants);

public record GeneratedVariantDto(int Id, string Barcode, string? Color, string? Size, decimal Price);

public class GenerateVariantsCommandHandler
    : IRequestHandler<GenerateVariantsCommand, GenerateVariantsResult>
{
    private readonly IApplicationDbContext _context;

    public GenerateVariantsCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<GenerateVariantsResult> Handle(GenerateVariantsCommand request, CancellationToken ct)
    {
        var colors = (request.Colors ?? new())
            .Select(ProductCatalog.NormalizeColor).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sizes = (request.Sizes ?? new())
            .Select(ProductCatalog.NormalizeSize).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (colors.Count == 0 || sizes.Count == 0)
            throw new InvalidOperationException("En az bir renk ve bir beden girin.");
        if (colors.Count * sizes.Count > 500)
            throw new InvalidOperationException("Tek seferde en fazla 500 varyant uretilebilir.");

        var product = await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
            ?? throw new KeyNotFoundException("Urun bulunamadi.");

        var catalog = new ProductCatalog(_context);
        var created = new List<(ProductVariant V, decimal Price)>();
        var skipped = 0;

        foreach (var color in colors)
        foreach (var size in sizes)
        {
            if (product.Variants.Any(v => !v.IsDeleted
                    && string.Equals(v.Color, color, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(v.Size, size, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }
            var price = request.ColorPrices != null
                        && request.ColorPrices.TryGetValue(color, out var cp) && cp > 0
                ? cp : request.Price;
            var v = await catalog.CreateVariantAsync(product, null, color, size, null, price, ct);
            created.Add((v, price));
        }

        await _context.SaveChangesAsync(ct);
        return new GenerateVariantsResult(created.Count, skipped,
            created.Select(c => new GeneratedVariantDto(c.V.Id, c.V.Barcode, c.V.Color, c.V.Size, c.Price)).ToList());
    }
}
