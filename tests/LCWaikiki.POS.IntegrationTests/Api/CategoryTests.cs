using LCWaikiki.POS.IntegrationTests.Infrastructure;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>Kategori yönetimi: oluşturma (kod önerisi), ad değiştirme, kod kuralları, silme koruması, yetki.</summary>
[Collection(ApiCollection.Name)]
public class CategoryTests
{
    private readonly PosApiFactory _factory;
    public CategoryTests(PosApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..6]}";

    private static string FreeCode()
    {
        // Testler paralel/tekrar çalışabilir: rastgele 2 harf (çakışırsa test kendi yeniden dener)
        var r = Random.Shared;
        return $"{(char)('A' + r.Next(26))}{(char)('A' + r.Next(26))}";
    }

    private static async Task<ApiResponse> CreateWithFreeCodeAsync(Scenario s, string name)
    {
        for (var i = 0; ; i++)
        {
            var res = await s.Api.Post("/api/categories", new { name, description = "test", code = FreeCode() }, s.AdminToken);
            if (res.Code != 409 || i > 20) return res;
        }
    }

    [Fact]
    public async Task Admin_creates_category_with_suggested_code_renames_and_deletes_empty_one()
    {
        var s = await CreateAsync(_factory);
        var name = Unique("Hirka");
        var created = await s.Api.Post("/api/categories", new { name, description = "  örgü  ", code = (string?)null }, s.AdminToken);
        Assert.Equal(201, created.Code);
        var id = created.Int("id");
        Assert.Matches("^[A-Z]{2}$", created.Str("code"));
        Assert.Equal("örgü", created.Str("description"));

        var renamed = await s.Api.Put($"/api/categories/{id}", new { name = name + " Yeni", description = (string?)null, code = (string?)null }, s.AdminToken);
        Assert.True(renamed.Ok, renamed.ToString());
        Assert.Equal(name + " Yeni", renamed.Str("name"));

        var list = await s.Api.Get("/api/categories/manage", s.AdminToken);
        var row = list.Body.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == id);
        Assert.Equal(0, row.GetProperty("productCount").GetInt32());
        Assert.True(row.GetProperty("canDelete").GetBoolean());
        Assert.True(row.GetProperty("canChangeCode").GetBoolean());

        Assert.Equal(204, (await s.Api.SendAsync(HttpMethod.Delete, $"/api/categories/{id}", null, s.AdminToken)).Code);
        var after = await s.Api.Get("/api/categories", s.AdminToken);
        Assert.DoesNotContain(after.Body.EnumerateArray(), c => c.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Duplicate_name_duplicate_code_and_invalid_input_are_rejected()
    {
        var s = await CreateAsync(_factory);
        var name = Unique("Yelek");
        var first = await CreateWithFreeCodeAsync(s, name);
        Assert.Equal(201, first.Code);

        Assert.Equal(409, (await s.Api.Post("/api/categories", new { name = name.ToUpperInvariant(), description = (string?)null, code = (string?)null }, s.AdminToken)).Code);
        Assert.Equal(409, (await s.Api.Post("/api/categories", new { name = Unique("Baska"), description = (string?)null, code = first.Str("code") }, s.AdminToken)).Code);
        Assert.Equal(400, (await s.Api.Post("/api/categories", new { name = "X", description = (string?)null, code = (string?)null }, s.AdminToken)).Code);
        Assert.Equal(400, (await s.Api.Post("/api/categories", new { name = Unique("Kod"), description = (string?)null, code = "A1" }, s.AdminToken)).Code);
    }

    [Fact]
    public async Task Category_with_products_cannot_be_deleted_and_its_code_is_locked()
    {
        var s = await CreateAsync(_factory);
        var created = await CreateWithFreeCodeAsync(s, Unique("Sort"));
        var id = created.Int("id");
        var product = await s.Api.Post("/api/products", new
        {
            name = "Kategori Testi " + Guid.NewGuid().ToString("N")[..6], description = (string?)null,
            categoryId = id, season = 2, seasonYear = 2026, department = 2, imagePath = (string?)null,
            variants = new[] { new { barcode = (string?)null, color = "Mavi", size = "M", imagePath = (string?)null, initialPrice = 50m } }
        }, s.AdminToken);
        Assert.Equal(201, product.Code);

        var del = await s.Api.SendAsync(HttpMethod.Delete, $"/api/categories/{id}", null, s.AdminToken);
        Assert.Equal(409, del.Code);
        Assert.Contains("1 ürün", del.Message);
        Assert.Equal(409, (await s.Api.Put($"/api/categories/{id}/code", new { code = FreeCode() }, s.AdminToken)).Code);

        var row = (await s.Api.Get("/api/categories/manage", s.AdminToken)).Body.EnumerateArray()
            .Single(c => c.GetProperty("id").GetInt32() == id);
        Assert.False(row.GetProperty("canDelete").GetBoolean());
        Assert.False(row.GetProperty("canChangeCode").GetBoolean());
    }

    [Fact]
    public async Task Only_admin_can_manage_categories()
    {
        var s = await CreateAsync(_factory);
        Assert.Equal(403, (await s.Api.Post("/api/categories", new { name = Unique("Mudur"), description = (string?)null, code = (string?)null }, s.Manager.Token)).Code);
        Assert.Equal(403, (await s.Api.Get("/api/categories/manage", s.Cashier.Token)).Code);
        Assert.True((await s.Api.Get("/api/categories", s.Cashier.Token)).Ok);   // liste herkese açık (ürün formları)
    }
}
