using Application.Features.Products.Common;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Urun kodu sistemine gecis: baslangicta (idempotent) eksikleri tamamlar.
///  • Kodu olmayan kategorilere 2 harfli kod verir (Erkek Giyim -> ER ...).
///  • Kodu olmayan urunlere sezon/yil/bolum tahmin edip kod uretir:
///      sezon: olusturma ayi Mart-Agustos = Yaz, digerleri Kis
///             (Ocak/Subat bir onceki yilin kis sezonudur)
///      bolum: urun / kategori adindaki Kadin/Erkek/Kiz/Erkek Cocuk/Bebek
///  • Renk grubuna bagli olmayan varyantlari kendi renk grubuna baglar.
/// Tahmin edilen degerler admin panelinde gorunur; yeni urunlerde zorunlu secilir.
/// </summary>
public static class ProductCatalogBackfill
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ProductCatalogBackfill");
        var catalog = new ProductCatalog(db);

        // 1) Kategori kodlari
        var categories = await db.Categories.IgnoreQueryFilters().OrderBy(c => c.Id).ToListAsync(ct);
        foreach (var c in categories.Where(c => !ProductCode.IsValidCategoryCode(c.Code)))
        {
            var used = categories.Where(x => x != c && x.Code != null).Select(x => x.Code!);
            c.Code = CategoryCodeSuggester.Suggest(c.Name, used);
            logger.LogInformation("Kategori kodu atandi: {Name} -> {Code}", c.Name, c.Code);
        }
        await db.SaveChangesAsync(ct);

        // 2) Urun kodlari
        var products = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Category)
            .Where(p => p.Code == null)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        foreach (var p in products)
        {
            var created = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt.ToLocalTime();
            var season = created.Month is >= 3 and <= 8 ? Season.Summer : Season.Winter;
            var year = season == Season.Winter && created.Month <= 2 ? created.Year - 1 : created.Year;
            var department = GuessDepartment(p.Name, p.Category?.Name);

            p.Season = season;
            p.SeasonYear = year;
            p.Department = department;
            p.AgeGroup = ProductCode.AgeGroupOf(department);
            p.Code = await catalog.GenerateProductCodeAsync(p.Category!, season, year, department, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Urun kodu atandi: #{Id} {Name} -> {Code}", p.Id, p.Name, p.Code);
        }

        // 3) Renk gruplari
        var ungrouped = await db.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.GroupId == null && v.Color != null)
            .ToListAsync(ct);
        foreach (var v in ungrouped)
            v.Group = await catalog.EnsureColorGroupAsync(v.Product, v.Color!, ct);
        if (ungrouped.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("{Count} varyant renk grubuna baglandi.", ungrouped.Count);
        }
    }

    internal static Department GuessDepartment(string? productName, string? categoryName)
    {
        foreach (var text in new[] { productName, categoryName })
        {
            var t = CategoryCodeSuggester.Fold(text ?? string.Empty).ToLowerInvariant();
            if (t.Contains("bebek")) return Department.Baby;
            if (t.Contains("erkek cocuk")) return Department.Boy;
            if (t.Contains("kiz")) return Department.Girl;
            if (t.Contains("kadin")) return Department.Women;
            if (t.Contains("erkek")) return Department.Men;
            if (t.Contains("cocuk")) return Department.Boy;
        }
        return Department.Women;
    }
}
