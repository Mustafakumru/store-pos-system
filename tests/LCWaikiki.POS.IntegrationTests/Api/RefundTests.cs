using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>İade, ödendiği yönteme: karta iade sınırı, nakde çevirme engeli, değişim farkı.</summary>
[Collection(ApiCollection.Name)]
public class RefundTests
{
    private const int CashMethod = 0, CardMethod = 1, GiftCardMethod = 2;
    private readonly PosApiFactory _factory;
    public RefundTests(PosApiFactory factory) => _factory = factory;

    private async Task<Scenario> OpenStoreAsync()
    {
        var s = await CreateAsync(_factory, 100m, 100m, 250m);
        await s.OpenSessionAsync(s.Cashier);
        return s;
    }

    [Fact]
    public async Task Card_refund_cap_deducts_previous_card_refunds_of_the_receipt()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier,
            new[] { Item(s.Variants[0]), Item(s.Variants[1]) }, new[] { Card(100), Cash(100) });
        var receipt = sale.Str("receiptNumber");

        var r1 = await s.RefundAsync(s.Cashier, receipt, s.Variants[0], CardMethod);
        var r2 = await s.RefundAsync(s.Cashier, receipt, s.Variants[1], CardMethod);

        Assert.True(r1.Ok && r2.Ok, $"{r1} | {r2}");
        Assert.Equal(100m, PayoutAmount(r1.Message, "Kart") + PayoutAmount(r2.Message, "Kart"));
        Assert.Equal(100m, PayoutAmount(r2.Message, "Nakit"));   // kartın payı bitti: kalan nakit payından
    }

    [Fact]
    public async Task Concurrent_refunds_on_same_receipt_cannot_exceed_card_paid_amount()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier,
            new[] { Item(s.Variants[0]), Item(s.Variants[1]) }, new[] { Card(100), Cash(100) });
        var receipt = sale.Str("receiptNumber");

        var results = await Task.WhenAll(
            s.RefundAsync(s.Cashier, receipt, s.Variants[0], CardMethod),
            s.RefundAsync(s.Cashier, receipt, s.Variants[1], CardMethod));

        Assert.All(results, r => Assert.True(r.Ok, r.ToString()));
        Assert.True(results.Sum(r => PayoutAmount(r.Message, "Kart")) <= 100m);
    }

    [Fact]
    public async Task Card_paid_sale_requested_as_cash_is_refunded_to_card()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[2]) }, new[] { Card(250) });

        var res = await s.RefundAsync(s.Cashier, sale.Str("receiptNumber"), s.Variants[2], CashMethod);

        Assert.True(res.Ok, res.ToString());
        Assert.Equal(250m, PayoutAmount(res.Message, "Kart"));
        Assert.Equal(0m, PayoutAmount(res.Message, "Nakit"));
    }

    [Fact]
    public async Task Gift_card_paid_sale_cannot_be_cashed_out_without_manager_approval()
    {
        var s = await OpenStoreAsync();
        var card = "GC" + Guid.NewGuid().ToString("N")[..10];
        var sold = await s.Api.Post("/api/pos/giftcards/sell",
            new { lines = new[] { new { cardNumber = card, amount = 600 } }, payments = new[] { Cash(600) } }, s.Cashier.Token);
        Assert.True(sold.Ok, sold.ToString());

        var a = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[2]) }, new[] { GiftCard(250, card) });
        var b = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[2]) }, new[] { GiftCard(250, card) });
        var receiptA = a.Str("receiptNumber");

        Assert.Equal(400, (await s.RefundAsync(s.Cashier, receiptA, s.Variants[2], CashMethod)).Code);
        Assert.Equal(400, (await s.RefundAsync(s.Cashier, receiptA, s.Variants[2], CardMethod)).Code);

        var toGiftCard = await s.RefundAsync(s.Cashier, receiptA, s.Variants[2], GiftCardMethod, new { giftCardNumber = card });
        Assert.True(toGiftCard.Ok, toGiftCard.ToString());

        var approved = await s.RefundAsync(s.Cashier, b.Str("receiptNumber"), s.Variants[2], CashMethod,
            new { refundApprovalToken = await s.ApprovalAsync("refund-cash", s.Cashier) });
        Assert.True(approved.Ok, approved.ToString());
        Assert.Equal(250m, PayoutAmount(approved.Message, "Nakit"));
    }

    [Fact]
    public async Task Exchange_difference_owed_to_customer_goes_to_original_tender()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[2]) }, new[] { Card(250) });

        var res = await s.Api.Post("/api/pos/exchange", new
        {
            returnItems = new[] { new { receiptNumber = sale.Str("receiptNumber"), variantId = s.Variants[2], refundQuantity = 1, returnReasonId = s.ReturnReasonId } },
            newItems = new[] { Item(s.Variants[0]) },
            settlementPayments = Array.Empty<object>(),
            owedRefundMethod = CashMethod
        }, s.Cashier.Token);

        Assert.True(res.Ok, res.ToString());
        Assert.Equal(150m, res.Dec("amountOwedToCustomer"));
        Assert.Equal(150m, PayoutAmount(res.Message, "Kart"));
    }

    [Fact]
    public async Task Concurrent_double_refund_of_same_item_succeeds_once()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) });
        var receipt = sale.Str("receiptNumber");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            s.RefundAsync(s.Cashier, receipt, s.Variants[0], CashMethod)));

        Assert.Equal(1, results.Count(r => r.Ok));
    }

    [Fact]
    public async Task Refund_rules_quantity_and_reason()
    {
        var s = await OpenStoreAsync();
        var receipt = (await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) })).Str("receiptNumber");

        var tooMany = await s.Api.Post($"/api/pos/sales/{receipt}/refund", new
        {
            items = new[] { new { variantId = s.Variants[0], refundQuantity = 5, returnReasonId = s.ReturnReasonId } }, reason = "x"
        }, s.Cashier.Token);
        Assert.Equal(400, tooMany.Code);

        var noReason = await s.Api.Post($"/api/pos/sales/{receipt}/refund", new
        {
            items = new[] { new { variantId = s.Variants[0], refundQuantity = 1 } }, reason = "x"
        }, s.Cashier.Token);
        Assert.Equal(400, noReason.Code);
    }

    [Fact]
    public async Task Receiptless_return_requires_manager_approval()
    {
        var s = await OpenStoreAsync();
        var res = await s.Api.Post("/api/pos/exchange", new
        {
            returnItems = new[] { new { receiptNumber = (string?)null, variantId = s.Variants[0], refundQuantity = 1, returnReasonId = s.ReturnReasonId, manualUnitPrice = 100 } },
            newItems = Array.Empty<object>(),
            settlementPayments = Array.Empty<object>()
        }, s.Cashier.Token);
        Assert.Equal(401, res.Code);
    }
}
