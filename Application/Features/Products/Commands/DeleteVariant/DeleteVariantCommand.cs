using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.DeleteVariant;

public record DeleteVariantCommand(int Id) : IRequest;

public class DeleteVariantCommandHandler
    : IRequestHandler<DeleteVariantCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteVariantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        DeleteVariantCommand request,
        CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.PriceHistories)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VariantId={request.Id} varyant bulunamadı.");

        // Aktif fiyatı kapat
        foreach (var price in variant.PriceHistories
            .Where(ph => ph.EndDate == null))
        {
            price.EndDate = DateTime.UtcNow;
        }

        variant.IsDeleted = true;
        variant.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }
}