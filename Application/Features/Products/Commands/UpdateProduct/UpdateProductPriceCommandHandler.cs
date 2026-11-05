using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.UpdateProductPrice;

public class UpdateProductPriceCommandHandler
    : IRequestHandler<UpdateProductPriceCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductPriceCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        UpdateProductPriceCommand request,
        CancellationToken cancellationToken)
    {
        if (request.NewPrice <= 0)
            throw new InvalidOperationException(
                "Fiyat 0'dan büyük olmalıdır.");

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VariantId={request.VariantId} varyant bulunamadı.");

        var now = DateTime.UtcNow;

        // Aktif fiyatı kapat
        var activePrice = await _context.PriceHistories
            .Where(ph => ph.VariantId == request.VariantId
                      && ph.EndDate == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePrice != null)
            activePrice.EndDate = now;

        // Yeni fiyat aç
        _context.PriceHistories.Add(new PriceHistory
        {
            VariantId = request.VariantId,
            Price = request.NewPrice,
            StartDate = now,
            EndDate = null
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}