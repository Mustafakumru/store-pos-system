using Application.Common.Interfaces;
using Application.Features.Products.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.UpdateVariant;

public class UpdateVariantCommandHandler
    : IRequestHandler<UpdateVariantCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateVariantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        UpdateVariantCommand request,
        CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VariantId={request.Id} varyant bulunamadı.");

        var color = ProductCatalog.NormalizeColor(request.Color);
        var size = ProductCatalog.NormalizeSize(request.Size);

        // Ayni urunde ayni Renk+Beden ikinci kez olamaz
        var clash = await _context.ProductVariants.AnyAsync(v =>
            v.Id != variant.Id && v.ProductId == variant.ProductId
            && v.Color == color && v.Size == size, cancellationToken);
        if (clash)
            throw new InvalidOperationException(
                $"Bu urunde '{color ?? "-"} / {size ?? "-"}' varyanti zaten var.");

        var colorChanged = !string.Equals(variant.Color, color, StringComparison.OrdinalIgnoreCase);
        variant.Color = color;
        variant.Size = size;
        variant.ImagePath = request.ImagePath;
        variant.IsActive = request.IsActive;

        // Renk degistiyse yeni rengin grubuna tasinir (gorseller renk bazli)
        if (colorChanged)
            variant.Group = color is null
                ? null
                : await new ProductCatalog(_context).EnsureColorGroupAsync(variant.Product, color, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
