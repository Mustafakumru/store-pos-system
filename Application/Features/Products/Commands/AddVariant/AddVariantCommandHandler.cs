using Application.Common.Interfaces;
using Application.Features.Products.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.AddVariant;

public class AddVariantCommandHandler
    : IRequestHandler<AddVariantCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AddVariantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        AddVariantCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"ID={request.ProductId} urun bulunamadi.");

        var catalog = new ProductCatalog(_context);
        var variant = await catalog.CreateVariantAsync(product, request.Barcode, request.Color,
            request.Size, request.ImagePath, request.InitialPrice, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return variant.Id;
    }
}
