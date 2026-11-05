using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class SalesTests
{
    private readonly PosApiFactory _factory;
    public SalesTests(PosApiFactory factory) => _factory = factory;

    private async Task<Scenario> OpenStoreAsync()
    {
        var s = await CreateAsync(_factory, 100m, 250m);
        await s.OpenSessionAsync(s.Cashier);
        return s;
    }

    [Fact]
    public async Task Cash_sale_with_change_records_applied_amount()
    {
        var s = await OpenStoreAsync();
        var res = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(150) });
        Assert.True(res.Ok, res.ToString());
        Assert.Equal(100m, res.Dec("finalAmount"));

        var x = await s.Api.Get("/api/pos/reports/x-report", s.Manager.Token);
        Assert.Equal(100m, x.Dec("cashTotal"));   // para üstü (50) kasaya sayılmaz
    }

    public static TheoryData<string> InvalidSales => new()
    {
        "card-overpayment", "underpayment", "zero-quantity", "absurd-quantity",
        "client-mixed-type", "unknown-variant", "unknown-customer", "unknown-gift-card"
    };

    [Theory]
    [MemberData(nameof(InvalidSales))]
    public async Task Invalid_sales_are_rejected_with_400(string kind)
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var res = kind switch
        {
            "card-overpayment" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Card(150) }),
            "underpayment" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(50) }),
            "zero-quantity" => await s.SaleAsync(s.Cashier, new[] { Item(v, 0) }, new[] { Cash(100) }),
            "absurd-quantity" => await s.SaleAsync(s.Cashier, new[] { Item(v, 5000) }, new[] { Cash(500000) }),
            "client-mixed-type" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new object[] { new { paymentType = 2, amount = 100 } }),
            "unknown-variant" => await s.SaleAsync(s.Cashier, new[] { Item(999_999) }, new[] { Cash(100) }),
            "unknown-customer" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) }, new { customerId = 999_999 }),
            "unknown-gift-card" => await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { GiftCard(100, "YOK-KART") }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.Equal(400, res.Code);
    }

    [Fact]
    public async Task Discount_requires_single_use_manager_approval()
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];

        var noApproval = await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(80) }, new { discountAmount = 20 });
        Assert.Equal(401, noApproval.Code);

        var token = await s.ApprovalAsync("discount", s.Cashier);
        var ok = await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(80) }, new { discountAmount = 20, approvalToken = token });
        Assert.True(ok.Ok, ok.ToString());
        Assert.Equal(80m, ok.Dec("finalAmount"));

        var replay = await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(80) }, new { discountAmount = 20, approvalToken = token });
        Assert.Equal(401, replay.Code);
    }

    [Fact]
    public async Task Approval_is_released_when_operation_fails_and_can_be_retried()
    {
        var s = await OpenStoreAsync();
        var token = await s.ApprovalAsync("discount", s.Cashier);

        var failed = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(1) }, new { discountAmount = 10, approvalToken = token });
        Assert.Equal(400, failed.Code);

        var retried = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(90) }, new { discountAmount = 10, approvalToken = token });
        Assert.True(retried.Ok, retried.ToString());
    }

    [Fact]
    public async Task Approval_of_another_cashier_is_rejected()
    {
        var s = await OpenStoreAsync();
        var other = await s.CreatePosUserAsync("Cashier", s.StoreId);
        await s.OpenSessionAsync(other);
        var token = await s.ApprovalAsync("discount", s.Cashier);

        var res = await s.SaleAsync(other, new[] { Item(s.Variants[0]) }, new[] { Cash(80) }, new { discountAmount = 20, approvalToken = token });
        Assert.Equal(401, res.Code);
    }

    [Fact]
    public async Task Concurrent_replay_of_one_discount_approval_succeeds_once()
    {
        var s = await OpenStoreAsync();
        var token = await s.ApprovalAsync("discount", s.Cashier);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(1) }, new { discountAmount = 99, approvalToken = token })));

        Assert.Equal(1, results.Count(r => r.Ok));
    }

    [Fact]
    public async Task Failed_sales_do_not_burn_receipt_numbers()
    {
        var s = await OpenStoreAsync();
        var v = s.Variants[0];
        var first = await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) });

        Assert.Equal(400, (await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Card(150) })).Code);
        Assert.Equal(400, (await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) }, new { couponCode = "YOKBOYLE" })).Code);
        Assert.Equal(400, (await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { GiftCard(100, "YOK") })).Code);

        var next = await s.SaleAsync(s.Cashier, new[] { Item(v) }, new[] { Cash(100) });
        Assert.Equal(Seq(first.Str("receiptNumber")) + 1, Seq(next.Str("receiptNumber")));
    }

    [Fact]
    public async Task Concurrent_sales_get_unique_consecutive_receipt_numbers()
    {
        var s = await OpenStoreAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) })));

        Assert.All(results, r => Assert.True(r.Ok, r.ToString()));
        var numbers = results.Select(r => Seq(r.Str("receiptNumber"))).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range(numbers[0], 10), numbers);
    }

    [Fact]
    public async Task Idempotency_key_replays_the_first_response()
    {
        var s = await OpenStoreAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { items = new[] { Item(s.Variants[0]) }, payments = new[] { Cash(100) } };

        var a = await s.Api.Post("/api/pos/sales", body, s.Cashier.Token, key);
        var b = await s.Api.Post("/api/pos/sales", body, s.Cashier.Token, key);

        Assert.True(a.Ok && b.Ok);
        Assert.Equal(a.Str("receiptNumber"), b.Str("receiptNumber"));
    }

    [Fact]
    public async Task Single_use_coupon_is_used_once_under_concurrency()
    {
        var s = await OpenStoreAsync();
        var code = "TEK" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var coupon = await s.Api.Post("/api/coupons", new
        {
            code, description = "tek kullanım", discountType = 0, value = 10,
            minPurchaseAmount = (decimal?)null, maxDiscountAmount = (decimal?)null,
            startDate = (DateTime?)null, endDate = (DateTime?)null, usageLimit = 1
        }, s.AdminToken);
        Assert.True(coupon.Ok, coupon.ToString());

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) }, new { couponCode = code })));

        Assert.Equal(1, results.Count(r => r.Ok));
    }

    private static int Seq(string receipt) => int.Parse(receipt[^4..]);
}
