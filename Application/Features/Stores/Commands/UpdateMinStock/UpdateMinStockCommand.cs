using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Commands.UpdateMinStock;

/// <summary>
/// Mağazadaki varyantların "düşük stok" eşiğini (MinStockAlert) günceller.
/// Tek varyant veya bir ürünün tüm varyantları tek istekte gönderilebilir.
/// Sadece mağazada stok satırı olan varyantlar güncellenir.
/// </summary>
public record UpdateMinStockCommand(
    int StoreId,
    List<MinStockItem> Items
) : IRequest<int>;

public record MinStockItem(int VariantId, int MinStockAlert);

public class UpdateMinStockCommandHandler : IRequestHandler<UpdateMinStockCommand, int>
{
    private const int MaxThreshold = 10_000;
    private readonly IApplicationDbContext _context;

    public UpdateMinStockCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(UpdateMinStockCommand request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("Güncellenecek ürün seçilmedi.");
        if (request.Items.Count > 1000)
            throw new InvalidOperationException("Tek seferde en fazla 1000 varyant güncellenebilir.");
        if (request.Items.Any(i => i.MinStockAlert < 0 || i.MinStockAlert > MaxThreshold))
            throw new InvalidOperationException($"Minimum stok 0 ile {MaxThreshold} arasında olmalıdır.");

        var wanted = request.Items
            .GroupBy(i => i.VariantId)
            .ToDictionary(g => g.Key, g => g.Last().MinStockAlert);

        var variantIds = wanted.Keys.ToList();
        var rows = await _context.StoreInventories
            .Where(si => si.StoreId == request.StoreId && variantIds.Contains(si.VariantId))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            throw new KeyNotFoundException("Mağazada bu ürünlere ait stok kaydı bulunamadı.");

        foreach (var row in rows)
            row.MinStockAlert = wanted[row.VariantId];

        await _context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }
}
