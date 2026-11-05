using LCWaikiki.POS.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>
/// Canlıya hazırlık turunda bulunan hataların kalıcı testleri: eşzamanlı para/stok
/// hareketleri, tekrar gönderilen istekler (Idempotency-Key), adet/tutar sınırları
/// ve ürün kodu üretimi.
/// </summary>
[Collection(ApiCollection.Name)]
public class HardeningTests
{
    private readonly PosApiFactory _factory;
    public HardeningTests(PosApiFactory factory) => _factory = factory;

    private async Task<Scenario> OpenStoreAsync()
    {
        var s = await CreateAsync(_factory, 100m, 250m);
        await s.OpenSessionAsync(s.Cashier);
        return s;
    }

    private static string NewCardNumber() => "HK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static Task<ApiResponse> SellGiftCardAsync(Scenario s, string card, decimal amount, string? idem = null)
        => s.Api.Post("/api/pos/giftcards/sell", new
        {
            lines = new[] { new { cardNumber = card, amount } },
            payments = new[] { Cash(amount) }
        }, s.Cashier.Token, idem);

    private Task<decimal> BalanceAsync(string card)
        => _factory.QueryDbAsync(db => db.GiftCards.Where(g => g.CardNumber == card).Select(g => g.Balance).SingleAsync());

    private Task<int> StockAsync(int storeId, int variantId)
        => _factory.QueryDbAsync(db => db.StoreInventories
            .Where(i => i.StoreId == storeId && i.VariantId == variantId).Select(i => i.Quantity).SingleAsync());

    // ── Eşzamanlılık ─────────────────────────────────────────────

    [Fact]
    public async Task Gift_card_balance_cannot_be_spent_twice_concurrently()
    {
        // Eskiden: 100 TL bakiyeli kartla aynı anda 5 satış -> 5'i de geçiyordu (500 TL harcama)
        var s = await OpenStoreAsync();
        var card = NewCardNumber();
        Assert.True((await SellGiftCardAsync(s, card, 100)).Ok);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { GiftCard(100, card) })));

        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.All(results.Where(r => !r.Ok), r => Assert.True(r.Code is 400 or 409, r.ToString()));
        Assert.Equal(0m, await BalanceAsync(card));
    }

    [Fact]
    public async Task Concurrent_sales_never_lose_stock_updates()
    {
        // Eskiden: eşzamanlı satışlar aynı stok satırını okuyup üzerine yazıyordu (kayıp güncelleme)
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var before = await StockAsync(s.StoreId, v);

        var results = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ =>
            s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) })));

        var sold = results.Count(r => r.Ok);
        Assert.True(sold == 15, string.Join(" | ", results.Where(r => !r.Ok).Select(r => r.ToString())));
        Assert.Equal(before - sold, await StockAsync(s.StoreId, v));
    }

    [Fact]
    public async Task Concurrent_sales_with_mixed_lines_keep_every_variant_consistent()
    {
        var s = await OpenStoreAsync();
        var (a, b) = (s.Variants[0], s.Variants[1]);
        var (beforeA, beforeB) = (await StockAsync(s.StoreId, a), await StockAsync(s.StoreId, b));

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => i % 2 == 0
            ? s.SaleAsync(s.Cashier, new[] { Item(a), Item(b) }, new[] { Cash(350) })
            : s.SaleAsync(s.Cashier, new[] { Item(b, 2) }, new[] { Cash(500) })));

        Assert.All(results, r => Assert.True(r.Ok, r.ToString()));
        Assert.Equal(beforeA - 5, await StockAsync(s.StoreId, a));
        Assert.Equal(beforeB - 5 - 10, await StockAsync(s.StoreId, b));
    }

    // ── Tekrar gönderilen istekler (Idempotency-Key) ─────────────

    [Fact]
    public async Task Gift_card_sale_retried_with_same_key_loads_balance_once()
    {
        var s = await OpenStoreAsync();
        var card = NewCardNumber();
        var key = Guid.NewGuid().ToString();

        var first = await SellGiftCardAsync(s, card, 150, key);
        var retry = await SellGiftCardAsync(s, card, 150, key);

        Assert.True(first.Ok && retry.Ok, retry.ToString());
        Assert.Equal(first.Str("documentNumber"), retry.Str("documentNumber"));
        Assert.Equal(150m, await BalanceAsync(card));
    }

    [Fact]
    public async Task Concurrent_requests_with_same_key_create_one_sale()
    {
        var s = await OpenStoreAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { items = new[] { Item(s.Variants[0]) }, payments = new[] { Cash(100) } };
        var before = await StockAsync(s.StoreId, s.Variants[0]);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            s.Api.Post("/api/pos/sales", body, s.Cashier.Token, key)));

        // Biri işler; diğerleri ya aynı yanıtı tekrar alır ya da "işleniyor" (409) görür
        Assert.All(results, r => Assert.True(r.Ok || r.Code == 409, r.ToString()));
        Assert.Single(results.Where(r => r.Ok).Select(r => r.Str("receiptNumber")).Distinct());
        Assert.Equal(before - 1, await StockAsync(s.StoreId, s.Variants[0]));
    }

    [Fact]
    public async Task Failed_request_releases_key_so_corrected_retry_succeeds()
    {
        var s = await OpenStoreAsync();
        var key = Guid.NewGuid().ToString();

        var underpaid = await s.Api.Post("/api/pos/sales",
            new { items = new[] { Item(s.Variants[0]) }, payments = new[] { Cash(50) } }, s.Cashier.Token, key);
        Assert.Equal(400, underpaid.Code);

        var corrected = await s.Api.Post("/api/pos/sales",
            new { items = new[] { Item(s.Variants[0]) }, payments = new[] { Cash(100) } }, s.Cashier.Token, key);
        Assert.True(corrected.Ok, corrected.ToString());
    }

    [Fact]
    public async Task Key_reused_on_a_different_endpoint_is_rejected()
    {
        var s = await OpenStoreAsync();
        var key = Guid.NewGuid().ToString();
        var sale = await s.Api.Post("/api/pos/sales",
            new { items = new[] { Item(s.Variants[0]) }, payments = new[] { Cash(100) } }, s.Cashier.Token, key);
        Assert.True(sale.Ok);

        var card = NewCardNumber();
        var other = await SellGiftCardAsync(s, card, 100, key);
        Assert.Equal(422, other.Code);
        Assert.Equal(0, await _factory.QueryDbAsync(db => db.GiftCards.CountAsync(g => g.CardNumber == card)));
    }

    // ── Adet / tutar sınırları ───────────────────────────────────

    public static TheoryData<string> OverLimit => new()
    {
        "sale-qty-1000", "sale-301-lines", "sale-payment-over-1m", "sale-21-payments",
        "exchange-new-qty-1000", "exchange-return-qty-1000", "refund-qty-1000"
    };

    [Theory]
    [MemberData(nameof(OverLimit))]
    public async Task Requests_over_business_limits_are_rejected_with_400(string kind)
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var receipt = (await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) })).Str("receiptNumber");
        var salesBefore = await _factory.QueryDbAsync(db => db.Sales.CountAsync());

        var res = kind switch
        {
            "sale-qty-1000" => await s.SaleAsync(s.Cashier, new[] { Item(v, 1000) }, new[] { Cash(100_000) }),
            "sale-301-lines" => await s.SaleAsync(s.Cashier, Enumerable.Repeat(Item(v), 301).ToArray(), new[] { Cash(30_100) }),
            "sale-payment-over-1m" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(2_000_000) }),
            "sale-21-payments" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, Enumerable.Repeat(Cash(5), 21).ToArray()),
            "exchange-new-qty-1000" => await s.Api.Post("/api/pos/exchange", new
            {
                returnItems = Array.Empty<object>(), newItems = new[] { Item(v, 1000) }, settlementPayments = Array.Empty<object>()
            }, s.Cashier.Token),
            "exchange-return-qty-1000" => await s.Api.Post("/api/pos/exchange", new
            {
                returnItems = new[] { new { receiptNumber = receipt, variantId = v, refundQuantity = 1000, returnReasonId = s.ReturnReasonId } },
                newItems = Array.Empty<object>(), settlementPayments = Array.Empty<object>()
            }, s.Cashier.Token),
            "refund-qty-1000" => await s.Api.Post($"/api/pos/sales/{receipt}/refund", new
            {
                items = new[] { new { variantId = v, refundQuantity = 1000, returnReasonId = s.ReturnReasonId } },
                reason = "test", payoutMethod = 0
            }, s.Cashier.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.Equal(400, res.Code);
        Assert.False(string.IsNullOrWhiteSpace(res.Message), res.ToString());
        Assert.Equal(salesBefore, await _factory.QueryDbAsync(db => db.Sales.CountAsync()));
    }

    // ── Ürün kodu ────────────────────────────────────────────────

    [Fact]
    public async Task Concurrently_created_products_get_unique_eight_char_codes_and_are_found_by_code()
    {
        var s = await OpenStoreAsync();
        var cats = await s.Api.Get("/api/categories", s.AdminToken);
        var categoryId = cats.Body[0].GetProperty("id").GetInt32();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => s.Api.Post("/api/products", new
        {
            name = $"Kod Testi {i} {Guid.NewGuid():N}"[..30], description = (string?)null,
            categoryId, season = 2, seasonYear = 2026, department = 2, imagePath = (string?)null,
            variants = new[] { new { barcode = (string?)null, color = "Siyah", size = "M", imagePath = (string?)null, initialPrice = 100m } }
        }, s.AdminToken)));

        Assert.All(results, r => Assert.True(r.Code == 201, r.ToString()));
        var codes = results.Select(r => r.Str("code")).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[WS][0-9][A-Z]{2}[0-9A-Z]{2}Z[148]$", c));
        Assert.All(codes, c => Assert.StartsWith("S6", c));   // Yaz 2026

        var found = await s.Api.Get($"/api/pos/products/by-code/{codes[0]}", s.Cashier.Token);
        Assert.True(found.Ok, found.ToString());
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var res = await new ApiClient(_factory.CreateClient()).Get("/api/health");
        Assert.True(res.Ok, res.ToString());
        Assert.Equal("ok", res.Str("status"));
    }
}
