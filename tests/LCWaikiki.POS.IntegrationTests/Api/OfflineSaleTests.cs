using LCWaikiki.POS.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>
/// Çevrimdışı satış: kasada sunucuya ulaşılamazken yapılan satışın sonradan gönderilmesi.
/// Satış anındaki fiyat, tekrar gönderim güvenliği, sunucu onayı gerektiren şeylerin
/// reddi ve fişe basılan çevrimdışı belge no ile iade.
/// </summary>
[Collection(ApiCollection.Name)]
public class OfflineSaleTests
{
    private readonly PosApiFactory _factory;
    public OfflineSaleTests(PosApiFactory factory) => _factory = factory;

    private static int _seq;
    private static string NewReference(int storeId)
        => $"9{storeId % 1000:000}{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}{Interlocked.Increment(ref _seq) % 10000:0000}";

    private async Task<Scenario> OpenStoreAsync()
    {
        var s = await CreateAsync(_factory, 100m, 250m);
        await s.OpenSessionAsync(s.Cashier);
        return s;
    }

    private static object OfflineItem(int variantId, decimal unitPrice, int quantity = 1)
        => new { variantId, quantity, offlineUnitPrice = unitPrice };

    private static Task<ApiResponse> OfflineSaleAsync(Scenario s, string reference, DateTime soldAtUtc,
        object[] items, object[] payments, object? extra = null, string? idem = null)
        => s.Api.Post("/api/pos/sales", Merge(new
        {
            items, payments,
            offline = new { reference, soldAt = soldAtUtc }
        }, extra), s.Cashier.Token, idem ?? Guid.NewGuid().ToString());

    private static Dictionary<string, object?> Merge(object a, object? b)
    {
        var d = new Dictionary<string, object?>();
        foreach (var o in new[] { a, b }.Where(x => x is not null))
            foreach (var p in o!.GetType().GetProperties()) d[p.Name] = p.GetValue(o);
        return d;
    }

    [Fact]
    public async Task Offline_sale_is_recorded_with_real_receipt_and_found_by_offline_reference()
    {
        var s = await OpenStoreAsync();
        var reference = NewReference(s.StoreId);
        var soldAt = DateTime.UtcNow;

        var res = await OfflineSaleAsync(s, reference, soldAt,
            new[] { OfflineItem(s.Variants[0], 100) }, new[] { Cash(100) });
        Assert.True(res.Ok, res.ToString());
        var receipt = res.Str("receiptNumber");
        Assert.NotEqual(reference, receipt);   // gerçek fiş no gönderimde atanır

        var sale = await _factory.QueryDbAsync(db => db.Sales.AsNoTracking().SingleAsync(x => x.ReceiptNumber == receipt));
        Assert.True(sale.IsOffline);
        Assert.Equal(reference, sale.OfflineReference);
        Assert.True(Math.Abs((sale.OfflineSoldAt!.Value - soldAt).TotalSeconds) < 1);

        var byOffline = await s.Api.Get($"/api/pos/sales/{reference}", s.Cashier.Token);
        Assert.True(byOffline.Ok, byOffline.ToString());
        Assert.Equal(receipt, byOffline.Str("receiptNumber"));
    }

    [Fact]
    public async Task Same_offline_document_sent_twice_creates_one_sale()
    {
        var s = await OpenStoreAsync();
        var reference = NewReference(s.StoreId);
        var soldAt = DateTime.UtcNow;
        object[] items = [OfflineItem(s.Variants[0], 100)];

        // Farklı Idempotency-Key ile de (örn. kuyruk dosyası kopyalandı) ikinci kayıt oluşmaz
        var a = await OfflineSaleAsync(s, reference, soldAt, items, new[] { Cash(100) });
        var b = await OfflineSaleAsync(s, reference, soldAt, items, new[] { Cash(100) });

        Assert.True(a.Ok && b.Ok, b.ToString());
        Assert.Equal(a.Str("receiptNumber"), b.Str("receiptNumber"));
        Assert.Equal(1, await _factory.QueryDbAsync(db => db.Sales.CountAsync(x => x.OfflineReference == reference)));
    }

    [Fact]
    public async Task Price_at_sale_time_is_used_and_recent_old_price_is_accepted_but_invented_price_is_not()
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var beforeChange = DateTime.UtcNow;
        await Task.Delay(1500);
        Assert.True((await s.Api.Put($"/api/products/variants/{v}/price", new { variantId = v, newPrice = 120m }, s.AdminToken)).Ok);

        // 1) Fiyat değişmeden önce yapılmış satış: o anki fiyat (100) geçerli
        var early = await OfflineSaleAsync(s, NewReference(s.StoreId), beforeChange.AddMilliseconds(500),
            new[] { OfflineItem(v, 100) }, new[] { Cash(100) });
        Assert.True(early.Ok, early.ToString());
        Assert.Equal(100m, early.Dec("finalAmount"));

        // 2) Değişiklikten sonra ama kasanın kataloğu eski (100): son 48 saatte geçerli olmuş fiyat kabul
        var stale = await OfflineSaleAsync(s, NewReference(s.StoreId), DateTime.UtcNow.AddSeconds(-1),
            new[] { OfflineItem(v, 100) }, new[] { Cash(100) });
        Assert.True(stale.Ok, stale.ToString());
        Assert.Equal(100m, stale.Dec("finalAmount"));

        // 3) Hiç geçerli olmamış fiyat: reddedilir
        var invented = await OfflineSaleAsync(s, NewReference(s.StoreId), DateTime.UtcNow.AddSeconds(-1),
            new[] { OfflineItem(v, 1) }, new[] { Cash(1) });
        Assert.Equal(400, invented.Code);
        Assert.Contains("geçerli değildi", invented.Message);

        // Normal (çevrimiçi) satış güncel fiyatı kullanır; istemcinin gönderdiği fiyat yok sayılır
        var online = await s.SaleAsync(s.Cashier, new[] { OfflineItem(v, 1) }, new[] { Cash(120) });
        Assert.True(online.Ok, online.ToString());
        Assert.Equal(120m, online.Dec("finalAmount"));
    }

    public static TheoryData<string> Rejected => new()
    {
        "coupon", "discount", "gift-card", "future", "too-old", "bad-reference"
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public async Task Things_that_need_the_server_are_rejected_for_offline_sales(string kind)
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var now = DateTime.UtcNow;
        object[] items = [OfflineItem(v, 100)];
        var res = kind switch
        {
            "coupon" => await OfflineSaleAsync(s, NewReference(s.StoreId), now, items, new[] { Cash(100) }, new { couponCode = "HERHANGI" }),
            "discount" => await OfflineSaleAsync(s, NewReference(s.StoreId), now,
                new object[] { new { variantId = v, quantity = 1, discountType = 0, discountValue = 10m, offlineUnitPrice = 100m } }, new[] { Cash(90) }),
            "gift-card" => await OfflineSaleAsync(s, NewReference(s.StoreId), now, items, new[] { GiftCard(100, "HK123") }),
            "future" => await OfflineSaleAsync(s, NewReference(s.StoreId), now.AddHours(2), items, new[] { Cash(100) }),
            "too-old" => await OfflineSaleAsync(s, NewReference(s.StoreId), now.AddDays(-8), items, new[] { Cash(100) }),
            "bad-reference" => await OfflineSaleAsync(s, "ab-12", now, items, new[] { Cash(100) }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.Equal(400, res.Code);
        Assert.False(string.IsNullOrWhiteSpace(res.Message));
    }

    [Fact]
    public async Task Customer_can_return_using_the_offline_document_number_on_their_receipt()
    {
        var s = await OpenStoreAsync();
        var reference = NewReference(s.StoreId);
        Assert.True((await OfflineSaleAsync(s, reference, DateTime.UtcNow,
            new[] { OfflineItem(s.Variants[1], 250) }, new[] { Cash(250) })).Ok);

        var refund = await s.RefundAsync(s.Cashier, reference, s.Variants[1], payoutMethod: 0);
        Assert.True(refund.Ok, refund.ToString());
        Assert.Equal(250m, PayoutAmount(refund.Message, "Nakit") is var p && p > 0 ? p : refund.Dec("refundAmount"));
    }

    [Fact]
    public async Task Offline_catalog_lists_sellable_variants_with_current_price()
    {
        var s = await OpenStoreAsync();
        var res = await s.Api.Get("/api/pos/offline-catalog", s.Cashier.Token);
        Assert.True(res.Ok, res.ToString());

        var item = res.Body.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("variantId").GetInt32() == s.Variants[1]);
        Assert.Equal(250m, item.GetProperty("price").GetDecimal());
        Assert.False(string.IsNullOrEmpty(item.GetProperty("barcode").GetString()));
        Assert.Matches("^[WS][0-9]", item.GetProperty("productCode").GetString());

        Assert.Equal(401, (await s.Api.Get("/api/pos/offline-catalog")).Code);
    }
}
