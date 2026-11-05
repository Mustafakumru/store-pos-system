using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.UpdateProduct;

public class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"ID={request.Id} ürün bulunamadı.");

        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.ImagePath = request.ImagePath;
        product.IsActive = request.IsActive;

        // Ürün pasife alınırsa tüm varyantlar da pasife alınır
        if (!request.IsActive)
        {
            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == request.Id)
                .ToListAsync(cancellationToken);

            foreach (var v in variants)
                v.IsActive = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}