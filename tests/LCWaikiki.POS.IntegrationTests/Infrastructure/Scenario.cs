using System.Text.RegularExpressions;

namespace LCWaikiki.POS.IntegrationTests.Infrastructure;

public sealed record PosUser(string UserId, string PersonelId, string Email, string Token, string Pin);

/// <summary>
/// Test senaryosu kurucusu. Her test KENDİ mağazasını, müdürünü, kasiyerlerini ve
/// stoklu ürünlerini oluşturur - testler birbirinin verisine dokunmaz ve paralel/
/// sırasız çalışabilir.
/// </summary>
public sealed class Scenario
{
    public const string AdminEmail = "admin@lcwaikiki.com";
    public const string AdminPassword = "Admin123!";   // Development seed

    public ApiClient Api { get; }
    public string AdminToken { get; private set; } = "";
    public int StoreId { get; private set; }
    public PosUser Manager { get; private set; } = null!;
    public PosUser Cashier { get; private set; } = null!;
    public List<int> Variants { get; } = new();
    public int ReturnReasonId { get; private set; }

    private static int _pinSeed = 1000;
    private readonly string _tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private Scenario(ApiClient api) => Api = api;

    /// <summary>Admin girişi + yeni mağaza + PIN'i belirlenmiş müdür ve kasiyer.</summary>
    public static async Task<Scenario> CreateAsync(PosApiFactory factory, params decimal[] variantPrices)
    {
        var s = new Scenario(new ApiClient(factory.CreateClient()));
        var login = await s.Api.Post("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        Assert.True(login.Ok, login.ToString());
        s.AdminToken = login.Str("token");

        s.StoreId = await s.CreateStoreAsync();
        s.Manager = await s.CreatePosUserAsync("StoreManager", s.StoreId);
        s.Cashier = await s.CreatePosUserAsync("Cashier", s.StoreId);

        var reasons = await s.Api.Get("/api/pos/return-reasons", s.Cashier.Token);
        s.ReturnReasonId = reasons.Body[0].GetProperty("id").GetInt32();

        if (variantPrices.Length > 0)
            await s.AddStockedProductAsync(variantPrices);
        return s;
    }

    public async Task<int> CreateStoreAsync()
    {
        var res = await Api.Post("/api/stores", new
        {
            name = "Test Mağazası " + _tag + Random.Shared.Next(1000),
            code = $"T-{_tag}-{Random.Shared.Next(100000)}",
            address = "Test Cad. 1", city = "İstanbul", phone = "0212 123 45 67"
        }, AdminToken);
        Assert.True(res.Code == 201, res.ToString());
        return res.Int("id");
    }

    /// <summary>Kullanıcı oluşturur, ilk POS girişini yapar, varsayılan PIN'i değiştirir.</summary>
    public async Task<PosUser> CreatePosUserAsync(string role, int storeId, bool changePin = true)
    {
        var email = $"{role.ToLowerInvariant()}{Guid.NewGuid():N}@test.com";
        var created = await Api.Post("/api/auth/create-user", new
        {
            email, password = role == "Cashier" ? null : "Manager123!",
            firstName = "Test", lastName = role == "Cashier" ? "Kasiyer" : "Müdür",
            role, storeId
        }, AdminToken);
        Assert.True(created.Ok, created.ToString());
        var personelId = created.Str("personelId");

        var first = await Api.Post("/api/pos/auth/login", new { personelId, pin = "1234" });
        Assert.True(first.Ok, first.ToString());
        if (!changePin)
            return new PosUser(created.Str("userId"), personelId, email, first.Str("token"), "1234");

        var pin = NextPin();
        var changed = await Api.Post("/api/pos/auth/change-pin",
            new { personelId, oldPin = "1234", newPin = pin }, first.Str("token"));
        Assert.True(changed.Ok, changed.ToString());
        return new PosUser(created.Str("userId"), personelId, email, changed.Str("token"), pin);
    }

    /// <summary>Ürün oluşturur, depoya alır, mağazaya sevk edip teslim alır (her varyanttan 100 adet).</summary>
    public async Task AddStockedProductAsync(params decimal[] prices)
    {
        var cats = await Api.Get("/api/categories", AdminToken);
        var product = await Api.Post("/api/products", new
        {
            name = "Test Ürün " + _tag, description = (string?)null,
            categoryId = cats.Body[0].GetProperty("id").GetInt32(),
            season = 1, seasonYear = 2026, department = 2, imagePath = (string?)null,
            variants = prices.Select((p, i) => new
            {
                barcode = (string?)null, color = "Renk" + i, size = "M", imagePath = (string?)null, initialPrice = p
            }).ToArray()
        }, AdminToken);
        Assert.True(product.Code == 201, product.ToString());
        var ids = product.Body.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).ToList();
        Variants.AddRange(ids);

        Assert.True((await Api.Post("/api/warehouse/receive",
            new { items = ids.Select(v => new { variantId = v, quantity = 200 }), notes = (string?)null }, AdminToken)).Ok);
        var tr = await Api.Post("/api/stocktransfers",
            new { toStoreId = StoreId, items = ids.Select(v => new { variantId = v, requestedQuantity = 100 }), notes = (string?)null }, AdminToken);
        var trId = tr.Int("id");
        Assert.True((await Api.Patch($"/api/stocktransfers/{trId}/ship",
            new { id = trId, items = ids.Select(v => new { variantId = v, shippedQuantity = 100 }) }, AdminToken)).Ok);
        Assert.True((await Api.Patch($"/api/stocktransfers/{trId}/receive", new { items = (object?)null, note = (string?)null }, Manager.Token)).Ok);
    }

    public async Task<string> ApprovalAsync(string action, PosUser requester)
    {
        var res = await Api.Post("/api/pos/manager-approval",
            new { personelId = Manager.PersonelId, pin = Manager.Pin, action }, requester.Token);
        Assert.True(res.Ok, res.ToString());
        return res.Str("approvalToken");
    }

    public async Task OpenSessionAsync(PosUser cashier, decimal openingFloat = 500)
    {
        var res = await Api.Post("/api/pos/cash-session/open",
            new { openingFloat, approvalToken = await ApprovalAsync("cash-session-open", cashier) }, cashier.Token);
        Assert.True(res.Ok, res.ToString());
    }

    // ── Satış / iade kısayolları ─────────────────────────────────
    public static object Item(int variantId, int quantity = 1) => new { variantId, quantity };
    public static object Cash(decimal amount) => new { paymentType = 0, amount };
    public static object Card(decimal amount) => new { paymentType = 1, amount };
    public static object GiftCard(decimal amount, string cardNumber) => new { paymentType = 3, amount, cardNumber };

    public Task<ApiResponse> SaleAsync(PosUser cashier, object[] items, object[] payments, object? extra = null)
        => Api.Post("/api/pos/sales", Merge(new { items, payments }, extra), cashier.Token);

    public Task<ApiResponse> RefundAsync(PosUser cashier, string receipt, int variantId, int payoutMethod, object? extra = null)
        => Api.Post($"/api/pos/sales/{receipt}/refund", Merge(new
        {
            items = new[] { new { variantId, refundQuantity = 1, returnReasonId = ReturnReasonId } },
            reason = "test",
            payoutMethod
        }, extra), cashier.Token);

    /// <summary>İade mesajındaki "Kart 100,00 TL" / "Nakit 250,00 TL" tutarı.</summary>
    public static decimal PayoutAmount(string? message, string label)
    {
        var m = Regex.Match(message ?? "", label + @" ([\d.,]+) TL");
        if (!m.Success) return 0;
        var v = m.Groups[1].Value;
        v = v.Contains(',') ? v.Replace(".", "").Replace(",", ".") : v;
        return decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, object?> Merge(object a, object? b)
    {
        var d = new Dictionary<string, object?>();
        foreach (var o in new[] { a, b }.Where(x => x is not null))
            foreach (var p in o!.GetType().GetProperties())
                d[p.Name] = p.GetValue(o);
        return d;
    }

    /// <summary>Kurallara uyan (tekrarsız, ardışık olmayan) benzersiz PIN.</summary>
    private static string NextPin()
    {
        while (true)
        {
            var n = Interlocked.Increment(ref _pinSeed) % 9000 + 1000;
            var pin = n.ToString();
            var seq = Enumerable.Range(1, 3).All(i => pin[i] - pin[i - 1] == 1)
                      || Enumerable.Range(1, 3).All(i => pin[i] - pin[i - 1] == -1);
            if (pin.Distinct().Count() > 1 && !seq) return pin;
        }
    }
}
