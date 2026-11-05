using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public record LastSaleDto(int Id, string ReceiptNumber, decimal FinalAmount, DateTime SaleDate, string Status);

public class PosService(ApiClient api)
{
    protected readonly ApiClient _api = api;

    public Task<ProcessSaleResponse> ProcessSaleAsync(
        ProcessSaleRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => _api.PostAsync<ProcessSaleResponse>("api/pos/sales", request, idempotencyKey, ct);

    public Task<GiftCardDto> GetGiftCardAsync(
        string cardNumber, CancellationToken ct = default)
        => _api.GetAsync<GiftCardDto>($"api/pos/giftcards/{cardNumber}", ct);

    public Task<PriceCheckDto> PriceCheckAsync(
        string barcode, CancellationToken ct = default)
        => _api.GetAsync<PriceCheckDto>($"api/pos/price-check/{barcode}", ct);

    /// <summary>
    /// Fiş numarasıyla satış sorgular ("Fişle Ara" akışında kullanılır).
    /// </summary>
    public Task<SaleReceiptDto> GetByReceiptAsync(
        string receiptNumber, CancellationToken ct = default)
        => _api.GetAsync<SaleReceiptDto>($"api/pos/sales/{receiptNumber}", ct);

    /// <summary>
    /// Fiş numarasıyla iade işler (tam/kısmi). ProcessRefundResult'ı döner -
    /// backend'in gerçekte hesapladığı RefundAmount'ı almak için (client-side
    /// tahminden daha güvenilir kaynak).
    /// </summary>
    public Task<ProcessRefundResult> RefundAsync(
        string receiptNumber, RefundRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => _api.PostAsync<ProcessRefundResult>(
            $"api/pos/sales/{receiptNumber}/refund", request, idempotencyKey, ct);

    /// <summary>
    /// Hediye kartına bakiye yükler (değişimde fişsiz iade farkını
    /// karşılamak için kullanılır).
    /// </summary>
    public Task<GiftCardDto> LoadGiftCardAsync(
        string cardNumber, LoadGiftCardRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => _api.PostAsync<GiftCardDto>(
            $"api/pos/giftcards/{cardNumber}/load", request, idempotencyKey, ct);

    /// <summary>
    /// Değişimi (iade + yeni satış) TEK atomik işlemde yapar.
    /// </summary>
    public Task<ProcessExchangeResult> ProcessExchangeAsync(
        ProcessExchangeRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => _api.PostAsync<ProcessExchangeResult>("api/pos/exchange", request, idempotencyKey, ct);

    /// <summary>
    /// Tüm aktif iade nedenlerini döner (20 sabit seed).
    /// </summary>
    public Task<List<ReturnReasonDto>> GetReturnReasonsAsync(CancellationToken ct = default)
        => _api.GetAsync<List<ReturnReasonDto>>("api/pos/return-reasons", ct);

    /// <summary>Son Belge Tekrari (1/2): kasiyerin son tamamlanan satisi (yoksa 404).</summary>
    public Task<LastSaleDto> GetLastSaleAsync(CancellationToken ct = default)
        => _api.GetAsync<LastSaleDto>("api/pos/reports/last-sale", ct);

    /// <summary>Son Belge Tekrari (2/2): mudur onayiyla fis verisini doner (kopya basilir).</summary>
    public Task<SaleReceiptDto> ReprintAsync(int saleId, string approvalToken, CancellationToken ct = default)
        => _api.PostAsync<SaleReceiptDto>($"api/pos/reports/reprint/{saleId}", new { approvalToken }, ct);

    /// <summary>
    /// Kupon kodunu verilen tutar için doğrular ve uygulanacak indirimi döner
    /// (ön izleme - satışta backend aynı kurallarla yeniden hesaplar).
    /// </summary>
    public Task<CouponValidationDto> ValidateCouponAsync(
        string code, decimal amount, CancellationToken ct = default)
        => _api.GetAsync<CouponValidationDto>(
            $"api/pos/coupons/{Uri.EscapeDataString(code)}/validate?amount=" +
            amount.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
}
// ── Askiya Al / Askidan Cagir ─────────────────────────────────────
public record SuspendCartResult(int Id, DateTime CreatedAt);
public record SuspendedCartDto(int Id, string CashierId, string? Note, DateTime CreatedAt);
public record ResumeCartResult(string CartDataJson);

/// <summary>Askidaki sepet (backend opak JSON olarak saklar, yapisini istemci belirler).</summary>
public class SuspendedCartData
{
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int InstallmentCount { get; set; }
    public string? SalesPersonId { get; set; }
    public string? Country { get; set; }
    public List<SuspendedCartLine> Lines { get; set; } = new();
}

public class SuspendedCartLine
{
    public int VariantId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? ImagePath { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public bool IsReturnItem { get; set; }
    public string? SourceReceiptNumber { get; set; }
    public int? ReturnReasonId { get; set; }
    public string? ReturnReasonName { get; set; }
    public int? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
}

public class SuspendedCartService(ApiClient api)
{
    private readonly ApiClient _api = api;

    public Task<SuspendCartResult> SuspendAsync(string cartDataJson, string? note, CancellationToken ct = default)
        => _api.PostAsync<SuspendCartResult>("api/pos/suspended-carts", new { cartDataJson, note }, ct);

    public Task<List<SuspendedCartDto>> GetAllAsync(CancellationToken ct = default)
        => _api.GetAsync<List<SuspendedCartDto>>("api/pos/suspended-carts", ct);

    public Task<ResumeCartResult> ResumeAsync(int id, CancellationToken ct = default)
        => _api.PostAsync<ResumeCartResult>($"api/pos/suspended-carts/{id}/resume", new { }, ct);
}
