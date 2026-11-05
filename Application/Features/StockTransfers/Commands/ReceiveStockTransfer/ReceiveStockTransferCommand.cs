using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockTransfers.Commands.ReceiveStockTransfer;

/// <param name="ActingStoreId">Mağaza müdürü için kendi mağazası - sadece o mağazaya
/// giden sevkiyatı teslim alabilir. Admin için null (kısıt yok).</param>
/// <param name="Items">Kalem bazında fiilen teslim alınan (satılabilir) miktarlar.
/// null/boş ise gönderilen miktarın tamamı teslim alınmış sayılır. Listede olmayan
/// kalem de tam teslim kabul edilir.</param>
/// <param name="Note">Eksik/hasarlı teslimde açıklama - eksik varsa zorunlu.</param>
public record ReceiveStockTransferCommand(
    int Id,
    int? ActingStoreId = null,
    List<ReceiveItem>? Items = null,
    string? Note = null) : IRequest;

public record ReceiveItem(int VariantId, int ReceivedQuantity);

public class ReceiveStockTransferCommandHandler
    : IRequestHandler<ReceiveStockTransferCommand>
{
    private readonly IApplicationDbContext _context;

    public ReceiveStockTransferCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        ReceiveStockTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(t => t.Items)
                .ThenInclude(i => i.Variant)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Sevkiyat bulunamadı.");

        if (request.ActingStoreId.HasValue && transfer.ToStoreId != request.ActingStoreId.Value)
            throw new Application.Common.Exceptions.ForbiddenAccessException(
                "Sadece mağazanıza gelen sevkiyatları teslim alabilirsiniz.");

        if (transfer.Status != TransferStatus.Shipped)
            throw new InvalidOperationException(
                "Sadece Shipped durumundaki sevkiyatlar teslim alınabilir.");

        var variantIds = transfer.Items.Select(i => i.VariantId).ToList();

        var warehouseStocks = await _context.WarehouseStocks
            .Where(w => variantIds.Contains(w.VariantId))
            .ToDictionaryAsync(w => w.VariantId, cancellationToken);

        var storeInventories = await _context.StoreInventories
            .Where(si => si.StoreId == transfer.ToStoreId
                      && variantIds.Contains(si.VariantId))
            .ToDictionaryAsync(si => si.VariantId, cancellationToken);

        // ── Teslim miktarlarını doğrula ─────────────────────────
        var received = new Dictionary<int, int>();
        foreach (var r in request.Items ?? [])
        {
            var item = transfer.Items.FirstOrDefault(i => i.VariantId == r.VariantId)
                ?? throw new InvalidOperationException(
                    $"Varyant {r.VariantId} bu sevkiyatta yok.");
            if (r.ReceivedQuantity < 0 || r.ReceivedQuantity > item.ShippedQuantity)
                throw new InvalidOperationException(
                    $"Teslim miktarı 0 ile gönderilen miktar ({item.ShippedQuantity}) arasında olmalıdır.");
            received[r.VariantId] = r.ReceivedQuantity;
        }

        var shortages = new List<string>();

        foreach (var item in transfer.Items)
        {
            var qty = received.TryGetValue(item.VariantId, out var q) ? q : item.ShippedQuantity;
            item.ReceivedQuantity = qty;
            if (qty < item.ShippedQuantity)
                shortages.Add($"{item.Variant?.Barcode ?? item.VariantId.ToString()}: -{item.ShippedQuantity - qty}");

            // Depo stoğu: gönderilen miktarın tamamı depodan çıkmıştır
            // (eksik/hasarlı kısım yolda kaybedilen/zayi sayılır, depoya geri dönmez).
            if (warehouseStocks.TryGetValue(item.VariantId, out var warehouseStock))
            {
                warehouseStock.Quantity -= item.ShippedQuantity;
                warehouseStock.ReservedQuantity = Math.Max(0, warehouseStock.ReservedQuantity - item.ShippedQuantity);
            }

            // Mağaza stoğu: sadece fiilen teslim alınan (satılabilir) miktar artar
            if (storeInventories.TryGetValue(item.VariantId, out var storeInventory))
            {
                storeInventory.Quantity += qty;
            }
            else if (qty > 0)   // hiç gelmeyen ürün için boş "tükendi" satırı açma
            {
                _context.StoreInventories.Add(new StoreInventory
                {
                    StoreId = transfer.ToStoreId,
                    VariantId = item.VariantId,
                    Quantity = qty
                });
            }
        }

        if (shortages.Count > 0 && string.IsNullOrWhiteSpace(request.Note))
            throw new InvalidOperationException(
                "Eksik veya hasarlı teslimde açıklama girilmesi zorunludur.");

        if (request.Note?.Trim().Length > 200)
            throw new InvalidOperationException("Açıklama en fazla 200 karakter olabilir.");

        if (shortages.Count > 0 || !string.IsNullOrWhiteSpace(request.Note))
        {
            var note = shortages.Count > 0
                ? $"Teslim farkı ({string.Join(", ", shortages)}): {request.Note!.Trim()}"
                : $"Teslim notu: {request.Note!.Trim()}";
            var combined = string.IsNullOrWhiteSpace(transfer.Notes)
                ? note
                : $"{transfer.Notes} | {note}";
            transfer.Notes = combined.Length > 500 ? combined[..497] + "..." : combined;   // kolon: max 500
        }

        transfer.Status = TransferStatus.Received;
        transfer.ReceivedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}