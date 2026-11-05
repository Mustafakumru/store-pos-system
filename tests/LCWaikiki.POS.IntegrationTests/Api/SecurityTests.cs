using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LCWaikiki.POS.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>Kimlik, PIN, token iptali ve kullanıcı yönetimi.</summary>
[Collection(ApiCollection.Name)]
public class SecurityTests
{
    private readonly PosApiFactory _factory;
    public SecurityTests(PosApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var api = new ApiClient(_factory.CreateClient());
        Assert.Equal(401, (await api.Get("/api/stores")).Code);
    }

    [Fact]
    public async Task Concurrently_created_users_get_unique_personel_ids()
    {
        var s = await Scenario.CreateAsync(_factory);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => s.Api.Post("/api/auth/create-user", new
        {
            email = $"esz{i}{Guid.NewGuid():N}@test.com", password = (string?)null,
            firstName = "Eş", lastName = "Zamanlı", role = "Cashier", storeId = s.StoreId
        }, s.AdminToken)));

        Assert.All(results, r => Assert.True(r.Ok, r.ToString()));
        var ids = results.Select(r => r.Str("personelId")).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Default_pin_token_can_only_change_pin_and_sequential_pin_is_rejected()
    {
        var s = await Scenario.CreateAsync(_factory);
        var user = await s.CreatePosUserAsync("Cashier", s.StoreId, changePin: false);

        Assert.Equal(403, (await s.Api.Get("/api/pos/return-reasons", user.Token)).Code);

        var sequential = await s.Api.Post("/api/pos/auth/change-pin",
            new { personelId = user.PersonelId, oldPin = "1234", newPin = "4567" }, user.Token);
        Assert.Equal(400, sequential.Code);
    }

    [Fact]
    public async Task Deactivation_invalidates_tokens_immediately_and_permanently()
    {
        var s = await Scenario.CreateAsync(_factory);
        var cashier = s.Cashier;
        Assert.True((await s.Api.Get("/api/pos/return-reasons", cashier.Token)).Ok);

        Assert.True((await s.Api.Patch($"/api/auth/users/{cashier.UserId}/toggle-status", null, s.AdminToken)).Ok);
        Assert.Equal(401, (await s.Api.Get("/api/pos/return-reasons", cashier.Token)).Code);

        Assert.True((await s.Api.Patch($"/api/auth/users/{cashier.UserId}/toggle-status", null, s.AdminToken)).Ok);
        Assert.Equal(401, (await s.Api.Get("/api/pos/return-reasons", cashier.Token)).Code);   // eski token geçersiz kalır

        var relogin = await s.Api.Post("/api/pos/auth/login", new { personelId = cashier.PersonelId, pin = cashier.Pin });
        Assert.True(relogin.Ok, relogin.ToString());
        Assert.True((await s.Api.Get("/api/pos/return-reasons", relogin.Str("token"))).Ok);
    }

    [Fact]
    public async Task Pin_reset_invalidates_existing_sessions()
    {
        var s = await Scenario.CreateAsync(_factory);
        Assert.True((await s.Api.Post($"/api/pos/auth/reset-pin/{s.Cashier.UserId}", null, s.Manager.Token)).Ok);
        Assert.Equal(401, (await s.Api.Get("/api/pos/return-reasons", s.Cashier.Token)).Code);
    }

    [Fact]
    public async Task Approval_token_cannot_be_used_as_bearer_token()
    {
        var s = await Scenario.CreateAsync(_factory);
        var approval = await s.ApprovalAsync("discount", s.Cashier);
        Assert.Equal(401, (await s.Api.Get("/api/categories", approval)).Code);
    }

    [Fact]
    public async Task Manager_of_another_store_cannot_approve()
    {
        var s = await Scenario.CreateAsync(_factory);
        var otherManager = await s.CreatePosUserAsync("StoreManager", await s.CreateStoreAsync());
        var res = await s.Api.Post("/api/pos/manager-approval",
            new { personelId = otherManager.PersonelId, pin = otherManager.Pin, action = "discount" }, s.Cashier.Token);
        Assert.Equal(403, res.Code);
    }

    [Fact]
    public async Task New_manager_can_use_web_panel_and_set_pin_there_but_cannot_approve_with_default_pin()
    {
        var s = await Scenario.CreateAsync(_factory);
        var email = $"panel{Guid.NewGuid():N}@test.com";
        var created = await s.Api.Post("/api/auth/create-user", new
        {
            email, password = "Manager123!", firstName = "Yeni", lastName = "Müdür", role = "StoreManager", storeId = s.StoreId
        }, s.AdminToken);
        var personelId = created.Str("personelId");

        var panel = await s.Api.Post("/api/auth/login", new { email, password = "Manager123!" });
        var token = panel.Str("token");
        var claims = Claims(token);
        Assert.False(claims.ContainsKey("mustChangePin"));
        Assert.Equal("true", claims["pinIsDefault"]);

        foreach (var url in new[] { $"/api/stores/{s.StoreId}", "/api/stocktransfers", "/api/auth/users", "/api/pos/reports/x-report" })
            Assert.True((await s.Api.Get(url, token)).Ok, url);

        var approve = await s.Api.Post("/api/pos/manager-approval",
            new { personelId, pin = "1234", action = "discount" }, s.Cashier.Token);
        Assert.Equal(403, approve.Code);

        var changed = await s.Api.Post("/api/pos/auth/change-pin", new { personelId, oldPin = "1234", newPin = "1357" }, token);
        Assert.True(changed.Ok, changed.ToString());
        Assert.False(Claims(changed.Str("token")).ContainsKey("pinIsDefault"));
    }

    [Fact]
    public async Task Pin_is_locked_after_five_failures()
    {
        var s = await Scenario.CreateAsync(_factory);
        var codes = new List<int>();
        for (var i = 0; i < 6; i++)
            codes.Add((await s.Api.Post("/api/pos/auth/login", new { personelId = s.Cashier.PersonelId, pin = "0000" })).Code);

        Assert.Equal(401, codes[0]);
        Assert.Equal(423, codes[^1]);
    }

    [Fact]
    public async Task Legacy_sha256_pin_still_works_and_is_upgraded_to_pbkdf2()
    {
        var s = await Scenario.CreateAsync(_factory);
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("2580LCW_POS_SALT")));
        await _factory.QueryDbAsync(db => db.Users
            .Where(u => u.Id == s.Cashier.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.PinHash, legacy)));

        var login = await s.Api.Post("/api/pos/auth/login", new { personelId = s.Cashier.PersonelId, pin = "2580" });
        Assert.True(login.Ok, login.ToString());

        var stored = await _factory.QueryDbAsync(db => db.Users.Where(u => u.Id == s.Cashier.UserId).Select(u => u.PinHash).SingleAsync());
        Assert.StartsWith("v2$", stored);
        Assert.True((await s.Api.Post("/api/pos/auth/login", new { personelId = s.Cashier.PersonelId, pin = "2580" })).Ok);
    }

    [Fact]
    public async Task Store_manager_create_user_is_forced_to_cashier_in_own_store()
    {
        var s = await Scenario.CreateAsync(_factory);
        var res = await s.Api.Post("/api/auth/create-user", new
        {
            email = $"esc{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Yetki", lastName = "Yükseltme",
            role = "Admin", storeId = 999_999
        }, s.Manager.Token);
        Assert.True(res.Ok, res.ToString());
        Assert.Equal("Cashier", res.Str("role"));
        Assert.Equal(s.StoreId, res.Int("storeId"));
    }

    private static Dictionary<string, string> Claims(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
    }
}
