using System.Text.Json;
using Application.Common;
using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>Admin satış performansı: net ciro (satış − iade), aylık hedef, orantılama, sıralama, yetki.</summary>
[Collection(ApiCollection.Name)]
public class SalesPerformanceTests
{
    private readonly PosApiFactory _factory;
    public SalesPerformanceTests(PosApiFactory factory) => _factory = factory;

    private static DateTime Today => BusinessClock.Today;
    private static DateTime MonthStart => new(Today.Year, Today.Month, 1);
    private static DateTime MonthEnd => MonthStart.AddMonths(1).AddDays(-1);
    private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

    private static JsonElement Row(ApiResponse res, int storeId)
        => res.Body.GetProperty("stores").EnumerateArray().Single(s => s.GetProperty("storeId").GetInt32() == storeId);

    private static decimal? Dec(JsonElement e, string prop)
        => e.GetProperty(prop).ValueKind == JsonValueKind.Null ? null : e.GetProperty(prop).GetDecimal();

    [Fact]
    public async Task Net_revenue_target_achievement_pace_and_ranking()
    {
        var a = await CreateAsync(_factory, 250m);
        await a.OpenSessionAsync(a.Cashier);
        var b = await a.Api.Post("/api/stores", new
        {
            name = "Perf B " + Guid.NewGuid().ToString("N")[..6], code = "PB-" + Random.Shared.Next(100000),
            address = "Test", city = "Bolu", phone = (string?)null
        }, a.AdminToken);
        Assert.Equal(201, b.Code);

        // A: 2 satış (500) − 1 iade (250) = 250 net; aylık hedef 1000 -> %25
        var s1 = await a.SaleAsync(a.Cashier, new[] { Item(a.Variants[0]) }, new[] { Cash(250) });
        await a.SaleAsync(a.Cashier, new[] { Item(a.Variants[0]) }, new[] { Cash(250) });
        Assert.True((await a.RefundAsync(a.Cashier, s1.Str("receiptNumber"), a.Variants[0], 0)).Ok);
        Assert.True((await a.Api.Put("/api/sales-targets", new { storeId = a.StoreId, year = Today.Year, month = Today.Month, targetAmount = 1000m }, a.AdminToken)).Ok);

        var res = await a.Api.Get($"/api/sales-performance?from={D(MonthStart)}&to={D(MonthEnd)}", a.AdminToken);
        Assert.True(res.Ok, res.ToString());

        var row = Row(res, a.StoreId);
        Assert.Equal(500m, row.GetProperty("salesAmount").GetDecimal());
        Assert.Equal(250m, row.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(250m, row.GetProperty("netRevenue").GetDecimal());
        Assert.Equal(2, row.GetProperty("saleCount").GetInt32());
        Assert.Equal(1000m, Dec(row, "targetAmount"));
        Assert.Equal(25.0m, Dec(row, "achievementPercent"));
        Assert.Equal(750m, Dec(row, "remainingToTarget"));
        var expected = Math.Round((decimal)Today.Day / DateTime.DaysInMonth(Today.Year, Today.Month) * 100, 1);
        Assert.Equal(expected, Dec(row, "expectedPercent"));
        var pace = row.GetProperty("pace").GetString();
        Assert.Equal(25m - expected >= 5 ? "Önde" : 25m - expected <= -5 ? "Geride" : "Hedefte", pace);

        // B: hedefi yok, satışı yok -> "Hedef yok", hedefi olanlardan sonra sıralanır
        var rowB = Row(res, b.Int("id"));
        Assert.Equal("Hedef yok", rowB.GetProperty("pace").GetString());
        Assert.Null(Dec(rowB, "achievementPercent"));
        Assert.True(rowB.GetProperty("rank").GetInt32() > row.GetProperty("rank").GetInt32());

        // Günlük seyir ve en çok satanlar
        var todayRow = res.Body.GetProperty("daily").EnumerateArray().Single(d => d.GetProperty("date").GetString() == D(Today));
        Assert.True(todayRow.GetProperty("saleCount").GetInt32() >= 2);
        Assert.Contains(res.Body.GetProperty("topProducts").EnumerateArray(), p => p.GetProperty("variantId").GetInt32() == a.Variants[0]);
        Assert.Equal(MonthEnd.Day, res.Body.GetProperty("days").GetInt32());
        Assert.Equal(D(MonthStart), res.Body.GetProperty("from").GetString());   // saatsiz yerel gün
    }

    [Fact]
    public async Task Monthly_target_is_prorated_for_shorter_periods()
    {
        var s = await CreateAsync(_factory);
        var monthly = 3100m;
        Assert.True((await s.Api.Put("/api/sales-targets", new { storeId = s.StoreId, year = Today.Year, month = Today.Month, targetAmount = monthly }, s.AdminToken)).Ok);

        var day = await s.Api.Get($"/api/sales-performance?from={D(Today)}&to={D(Today)}", s.AdminToken);
        var expected = Math.Round(monthly / DateTime.DaysInMonth(Today.Year, Today.Month), 2);
        Assert.Equal(expected, Dec(Row(day, s.StoreId), "targetAmount"));

        // Dönem gelecek aya taşarsa ve o ayın hedefi yoksa: hedef gösterilmez (yanıltıcı oran yok)
        var cross = await s.Api.Get($"/api/sales-performance?from={D(MonthEnd)}&to={D(MonthEnd.AddDays(1))}", s.AdminToken);
        Assert.Null(Dec(Row(cross, s.StoreId), "targetAmount"));
    }

    [Fact]
    public async Task Targets_can_be_listed_updated_and_removed_by_admin_only()
    {
        var s = await CreateAsync(_factory);
        var body = new { storeId = s.StoreId, year = 2027, month = 3, targetAmount = 150000m };
        Assert.True((await s.Api.Put("/api/sales-targets", body, s.AdminToken)).Ok);
        Assert.True((await s.Api.Put("/api/sales-targets", body with { targetAmount = 175000m }, s.AdminToken)).Ok);

        var list = await s.Api.Get("/api/sales-targets?year=2027&month=3", s.AdminToken);
        var item = list.Body.EnumerateArray().Single(t => t.GetProperty("storeId").GetInt32() == s.StoreId);
        Assert.Equal(175000m, item.GetProperty("targetAmount").GetDecimal());

        Assert.True((await s.Api.Put("/api/sales-targets", body with { targetAmount = 0m }, s.AdminToken)).Ok);
        list = await s.Api.Get("/api/sales-targets?year=2027&month=3", s.AdminToken);
        item = list.Body.EnumerateArray().Single(t => t.GetProperty("storeId").GetInt32() == s.StoreId);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("targetAmount").ValueKind);

        Assert.Equal(400, (await s.Api.Put("/api/sales-targets", body with { month = 13 }, s.AdminToken)).Code);
        Assert.Equal(400, (await s.Api.Put("/api/sales-targets", body with { targetAmount = -5m }, s.AdminToken)).Code);
        Assert.Equal(403, (await s.Api.Put("/api/sales-targets", body, s.Manager.Token)).Code);
        Assert.Equal(403, (await s.Api.Get($"/api/sales-performance?from={D(Today)}&to={D(Today)}", s.Manager.Token)).Code);
        Assert.Equal(400, (await s.Api.Get($"/api/sales-performance?from={D(Today)}&to={D(Today.AddDays(-1))}", s.AdminToken)).Code);
    }
}
