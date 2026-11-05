using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Services;

/// <summary>
/// Hediye karti SATISI (ucretli yukleme). EMANET olarak izlenir: ciroya
/// girmez, kart harcandiginda ciro olusur. Acik kasa oturumu gerekir.
/// </summary>
public class GiftCardService(ApiClient api)
{
    protected readonly ApiClient _api = api;

    public const decimal MaxPerCard = 10_000m;

    public Task<SellGiftCardResult> SellAsync(
        List<GiftCardSaleLineRequest> lines, List<SalePaymentRequest> payments,
        string? idempotencyKey = null, CancellationToken ct = default)
        => _api.PostAsync<SellGiftCardResult>("api/pos/giftcards/sell", new { lines, payments }, idempotencyKey, ct);
}

public record GiftCardSaleLineRequest(string CardNumber, decimal Amount);

public record SoldCardDto(string CardNumber, decimal LoadedAmount, decimal NewBalance, DateTime ExpiryDate);

public record SellGiftCardResult(
    int Id, string DocumentNumber, decimal TotalAmount, decimal ChangeAmount,
    DateTime SoldAt, List<SoldCardDto> Cards);
