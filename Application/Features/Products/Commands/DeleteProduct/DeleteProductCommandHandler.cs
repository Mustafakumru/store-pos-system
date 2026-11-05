using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.DeleteProduct;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Soft delete — fiziksel silme YOK
        // Global Query Filter bu kaydı artık tüm sorgulardan otomatik gizler
        product.IsDeleted = true;
        product.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}