using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class CashSessionTests
{
    private readonly PosApiFactory _factory;
    public CashSessionTests(PosApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Same_approval_token_used_concurrently_opens_exactly_one_session()
    {
        var s = await CreateAsync(_factory);
        var token = await s.ApprovalAsync("cash-session-open", s.Cashier);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            s.Api.Post("/api/pos/cash-session/open", new { openingFloat = 500, approvalToken = token }, s.Cashier.Token)));

        Assert.Equal(1, results.Count(r => r.Ok));
    }

    [Fact]
    public async Task Different_tokens_concurrently_still_open_one_session_db_constraint()
    {
        var s = await CreateAsync(_factory);
        var tokens = new List<string>();
        for (var i = 0; i < 4; i++) tokens.Add(await s.ApprovalAsync("cash-session-open", s.Cashier));

        var results = await Task.WhenAll(tokens.Select(t =>
            s.Api.Post("/api/pos/cash-session/open", new { openingFloat = 0, approvalToken = t }, s.Cashier.Token)));

        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.All(results.Where(r => !r.Ok), r => Assert.Equal(400, r.Code));
        Assert.Contains(results, r => r.Message?.Contains("Zaten açık") == true);
    }

    [Fact]
    public async Task Sale_without_open_session_is_rejected()
    {
        var s = await CreateAsync(_factory, 100m);
        var res = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) });
        Assert.Equal(400, res.Code);
        Assert.Contains("Kasa açılmadan", res.Message);
    }

    [Fact]
    public async Task Z_report_refused_while_drawer_open_and_created_after_close()
    {
        var s = await CreateAsync(_factory, 100m);
        await s.OpenSessionAsync(s.Cashier);
        Assert.True((await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) })).Ok);

        var blocked = await s.Api.Post("/api/pos/reports/z-report", new { }, s.Manager.Token);
        Assert.Equal(400, blocked.Code);

        var close = await s.Api.Post("/api/pos/cash-session/close",
            new { closingAmount = 600, approvalToken = await s.ApprovalAsync("cash-session-close", s.Cashier) }, s.Cashier.Token);
        Assert.True(close.Ok, close.ToString());

        var z1 = await s.Api.Post("/api/pos/reports/z-report", new { }, s.Manager.Token);
        Assert.True(z1.Ok, z1.ToString());
        Assert.Equal(100m, z1.Dec("totalSalesAmount"));

        var z2 = await s.Api.Post("/api/pos/reports/z-report", new { }, s.Manager.Token);
        Assert.Equal(z1.Int("reportNumber") + 1, z2.Int("reportNumber"));
        Assert.Equal(0m, z2.Dec("totalSalesAmount"));
    }

    [Fact]
    public async Task Manager_sees_cashier_expected_cash_cashier_and_other_store_manager_cannot()
    {
        var s = await CreateAsync(_factory, 100m);
        await s.OpenSessionAsync(s.Cashier, 500);
        Assert.True((await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(150) })).Ok);

        var url = $"/api/pos/cash-session/report?cashierId={s.Cashier.UserId}";
        var report = await s.Api.Get(url, s.Manager.Token);
        Assert.True(report.Ok, report.ToString());
        Assert.Equal(600m, report.Dec("expectedCashAmount"));   // 500 avans + 100 (para üstü düşülmüş)

        Assert.Equal(403, (await s.Api.Get(url, s.Cashier.Token)).Code);

        var otherStore = await s.CreateStoreAsync();
        var otherManager = await s.CreatePosUserAsync("StoreManager", otherStore);
        Assert.Equal(400, (await s.Api.Get(url, otherManager.Token)).Code);
    }
}
