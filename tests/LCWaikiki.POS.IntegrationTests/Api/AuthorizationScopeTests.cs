using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>Mağaza kapsamı (IDOR), kişisel veri maskeleme, kör sayım.</summary>
[Collection(ApiCollection.Name)]
public class AuthorizationScopeTests
{
    private readonly PosApiFactory _factory;
    public AuthorizationScopeTests(PosApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Manager_sees_only_own_store()
    {
        var s = await CreateAsync(_factory);
        var other = await s.CreateStoreAsync();

        Assert.True((await s.Api.Get($"/api/stores/{s.StoreId}", s.Manager.Token)).Ok);
        Assert.Equal(403, (await s.Api.Get($"/api/stores/{other}", s.Manager.Token)).Code);
        Assert.Equal(403, (await s.Api.Get($"/api/stores/{other}/inventory", s.Manager.Token)).Code);
        Assert.True((await s.Api.Get($"/api/stores/{other}", s.AdminToken)).Ok);
        Assert.Equal(404, (await s.Api.Get($"/api/auth/{other}", s.Manager.Token)).Code);   // kaldırılan uç
    }

    [Fact]
    public async Task Role_and_store_boundaries_on_stock_operations()
    {
        var s = await CreateAsync(_factory, 100m);
        var other = await s.CreateStoreAsync();

        var foreignTransfer = await s.Api.Post("/api/stocktransfers",
            new { toStoreId = other, items = new[] { new { variantId = s.Variants[0], requestedQuantity = 1 } }, notes = (string?)null }, s.Manager.Token);
        Assert.Equal(403, foreignTransfer.Code);

        var cashierReceive = await s.Api.Post("/api/warehouse/receive",
            new { items = new[] { new { variantId = s.Variants[0], quantity = 1 } }, notes = (string?)null }, s.Cashier.Token);
        Assert.Equal(403, cashierReceive.Code);
    }

    [Fact]
    public async Task Cross_store_receipt_masks_customer_data_same_store_sees_all()
    {
        var s = await CreateAsync(_factory, 100m);
        await s.OpenSessionAsync(s.Cashier);
        var phone = "53" + Random.Shared.NextInt64(10_000_000, 99_999_999);
        var customer = await s.Api.Post("/api/pos/customers",
            new { phoneNumber = phone, fullName = "Zeynep Kaya", email = (string?)null, smsConsent = false }, s.Cashier.Token);
        Assert.Equal(201, customer.Code);
        var receipt = (await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) },
            new { customerId = customer.Int("id") })).Str("receiptNumber");

        var otherCashier = await s.CreatePosUserAsync("Cashier", await s.CreateStoreAsync());
        var masked = await s.Api.Get($"/api/pos/sales/{receipt}", otherCashier.Token);
        Assert.True(masked.Ok, masked.ToString());
        Assert.Equal("Z***** K***", masked.Str("customerName"));
        Assert.Equal("******" + phone[^4..], masked.Str("customerPhone"));
        Assert.Equal("", masked.Str("cashierId"));
        Assert.Equal(1, masked.Body.GetProperty("items").GetArrayLength());   // iade için kalemler görünür

        var full = await s.Api.Get($"/api/pos/sales/{receipt}", s.Cashier.Token);
        Assert.Equal("Zeynep Kaya", full.Str("customerName"));
    }

    [Fact]
    public async Task X_report_hides_cash_from_cashier_blind_count()
    {
        var s = await CreateAsync(_factory, 100m);
        await s.OpenSessionAsync(s.Cashier);
        Assert.True((await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) })).Ok);

        var cashier = await s.Api.Get("/api/pos/reports/x-report", s.Cashier.Token);
        Assert.True(cashier.Bool("cashDetailsHidden"));
        Assert.Equal(0m, cashier.Dec("cashTotal"));
        Assert.Equal(0m, cashier.Dec("netCash"));
        Assert.Equal(0m, cashier.Dec("totalSalesAmount"));
        Assert.Equal(1, cashier.Int("transactionCount"));

        var manager = await s.Api.Get("/api/pos/reports/x-report", s.Manager.Token);
        Assert.False(manager.Bool("cashDetailsHidden"));
        Assert.Equal(100m, manager.Dec("cashTotal"));
    }
}
