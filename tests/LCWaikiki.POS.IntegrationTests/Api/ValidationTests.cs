using LCWaikiki.POS.IntegrationTests.Infrastructure;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>FluentValidation pipeline: 400 + alan bazlı "errors" listesi.</summary>
[Collection(ApiCollection.Name)]
public class ValidationTests
{
    private readonly PosApiFactory _factory;
    public ValidationTests(PosApiFactory factory) => _factory = factory;

    private static void AssertInvalid(ApiResponse res, params string[] fields)
    {
        Assert.True(res.Code == 400, res.ToString());
        Assert.False(string.IsNullOrEmpty(res.Message));
        foreach (var f in fields) Assert.Contains(f, res.ErrorFields);
    }

    private static object Store(string name, string code, string? phone = null)
        => new { name, code, address = (string?)null, city = (string?)null, phone };

    [Fact]
    public async Task Store_rules()
    {
        var s = await Scenario.CreateAsync(_factory);
        AssertInvalid(await s.Api.Post("/api/stores", Store("", ""), s.AdminToken), "name", "code");
        AssertInvalid(await s.Api.Post("/api/stores", Store("Test", "kod boşluklu!"), s.AdminToken), "code");
        AssertInvalid(await s.Api.Post("/api/stores", Store("Test", "V-1", "abc"), s.AdminToken), "phone");
        AssertInvalid(await s.Api.Post("/api/stores", Store(new string('x', 151), "V-2"), s.AdminToken), "name");
        AssertInvalid(await s.Api.Put($"/api/stores/{s.StoreId}",
            new { id = s.StoreId, name = " ", address = (string?)null, city = (string?)null, phone = (string?)null }, s.AdminToken), "name");

        var ok = await s.Api.Put($"/api/stores/{s.StoreId}",
            new { id = s.StoreId, name = "Yeni Ad", address = (string?)null, city = "Ankara", phone = "+90 312 123 45 67" }, s.AdminToken);
        Assert.Equal(204, ok.Code);
    }

    [Fact]
    public async Task Each_field_reports_only_its_first_error()
    {
        var s = await Scenario.CreateAsync(_factory);
        var res = await s.Api.Post("/api/stores", Store("", ""), s.AdminToken);
        Assert.Equal(1, res.Body.GetProperty("errors").GetProperty("name").GetArrayLength());
    }

    [Fact]
    public async Task User_names_must_be_letters_turkish_allowed()
    {
        var s = await Scenario.CreateAsync(_factory);
        object User(string first, string last) => new
        {
            email = $"v{Guid.NewGuid():N}@test.com", password = (string?)null, firstName = first, lastName = last,
            role = "Cashier", storeId = s.StoreId
        };
        Assert.Equal(400, (await s.Api.Post("/api/auth/create-user", User("", "X"), s.AdminToken)).Code);
        Assert.Equal(400, (await s.Api.Post("/api/auth/create-user", User("Ali1", "<script>"), s.AdminToken)).Code);
        Assert.True((await s.Api.Post("/api/auth/create-user", User("Şükrü", "Öztürk-Çelik"), s.AdminToken)).Ok);
    }

    [Fact]
    public async Task Customer_rules_and_phone_normalization()
    {
        var s = await Scenario.CreateAsync(_factory);
        var t = s.Cashier.Token;
        AssertInvalid(await s.Api.Post("/api/pos/customers",
            new { phoneNumber = "abc", fullName = "", email = "notmail", smsConsent = false }, t), "phoneNumber", "fullName", "email");

        var tail = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var created = await s.Api.Post("/api/pos/customers", new
        {
            phoneNumber = $"0 (555) {tail[..3]} {tail[3..5]} {tail[5..]}", fullName = " Ayşe Kaya ", email = " ayse@ornek.com ", smsConsent = true
        }, t);
        Assert.Equal(201, created.Code);
        Assert.Equal("555" + tail, created.Str("phoneNumber"));
        Assert.Equal("Ayşe Kaya", created.Str("fullName"));

        var duplicate = await s.Api.Post("/api/pos/customers",
            new { phoneNumber = "+90555" + tail, fullName = "Başka Kişi", email = (string?)null, smsConsent = false }, t);
        Assert.Equal(409, duplicate.Code);

        foreach (var q in new[] { "0555" + tail, "555" + tail, "+90 555 " + tail })
        {
            var found = await s.Api.Get("/api/pos/customers/search?phone=" + Uri.EscapeDataString(q), t);
            Assert.Equal(created.Int("id"), found.Int("id"));
        }
    }

    [Fact]
    public async Task Suspended_cart_rules()
    {
        var s = await Scenario.CreateAsync(_factory);
        var t = s.Cashier.Token;
        AssertInvalid(await s.Api.Post("/api/pos/suspended-carts", new { cartDataJson = new string('x', 2_000_000), note = (string?)null }, t), "cartDataJson");
        AssertInvalid(await s.Api.Post("/api/pos/suspended-carts", new { cartDataJson = "bu json değil", note = (string?)null }, t), "cartDataJson");
        AssertInvalid(await s.Api.Post("/api/pos/suspended-carts", new { cartDataJson = "{}", note = new string('n', 201) }, t), "note");

        var bigButNormal = System.Text.Json.JsonSerializer.Serialize(new { items = Enumerable.Repeat(new { barcode = "8684040000019", qty = 2 }, 300) });
        Assert.True((await s.Api.Post("/api/pos/suspended-carts", new { cartDataJson = bigButNormal, note = "müşteri döner" }, t)).Ok);
    }

    [Fact]
    public async Task Coupon_rules()
    {
        var s = await Scenario.CreateAsync(_factory);
        object Coupon(string code, int type, decimal value, int? limit = null, DateTime? start = null, DateTime? end = null) => new
        {
            code, description = (string?)null, discountType = type, value,
            minPurchaseAmount = (decimal?)null, maxDiscountAmount = (decimal?)null, startDate = start, endDate = end, usageLimit = limit
        };
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        AssertInvalid(await s.Api.Post("/api/coupons", Coupon("P" + tag, 1, 150), s.AdminToken), "value");     // %150
        AssertInvalid(await s.Api.Post("/api/coupons", Coupon("kötü kod!", 0, 10, limit: 0), s.AdminToken), "code", "usageLimit");
        AssertInvalid(await s.Api.Post("/api/coupons", Coupon("D" + tag, 0, 10,
            start: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), end: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)), s.AdminToken), "endDate");

        Assert.True((await s.Api.Post("/api/coupons", Coupon("A" + tag, 0, 150), s.AdminToken)).Ok);         // 150 TL sabit: geçerli
        var dup = await s.Api.Post("/api/coupons", Coupon("A" + tag, 0, 10), s.AdminToken);
        Assert.Equal(400, dup.Code);
        Assert.Contains("zaten var", dup.Message);
    }

    [Fact]
    public async Task Product_and_variant_rules()
    {
        var s = await Scenario.CreateAsync(_factory, 100m);
        var cats = await s.Api.Get("/api/categories", s.AdminToken);
        var categoryId = cats.Body[0].GetProperty("id").GetInt32();
        var variantId = s.Variants[0];
        var productId = await _factory.QueryDbAsync(db =>
            Task.FromResult(db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).Single()));

        AssertInvalid(await s.Api.Put($"/api/products/{productId}", new
        {
            id = productId, name = "", description = new string('d', 1001), categoryId, imagePath = (string?)null, isActive = true
        }, s.AdminToken), "name", "description");
        Assert.Equal(204, (await s.Api.Put($"/api/products/{productId}", new
        {
            id = productId, name = "Yeni Ürün Adı", description = (string?)null, categoryId, imagePath = (string?)null, isActive = true
        }, s.AdminToken)).Code);

        AssertInvalid(await s.Api.Post($"/api/products/{productId}/variants", new
        {
            productId, barcode = (string?)null, color = new string('c', 51), size = "XL", imagePath = (string?)null, initialPrice = 0
        }, s.AdminToken), "color", "initialPrice");

        AssertInvalid(await s.Api.Put($"/api/products/variants/{variantId}/price", new { variantId, newPrice = 5_000_000 }, s.AdminToken), "newPrice");
        Assert.Equal(204, (await s.Api.Put($"/api/products/variants/{variantId}/price", new { variantId, newPrice = 129.9 }, s.AdminToken)).Code);

        AssertInvalid(await s.Api.Post($"/api/products/{productId}/groups", new { groupName = "", groupType = "Color" }, s.AdminToken), "groupName");
    }
}
