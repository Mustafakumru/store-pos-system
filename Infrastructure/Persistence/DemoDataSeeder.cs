using Application.Common;
using Application.Features.Products.Commands.CreateProduct;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// DEMO VERİSİ (yalnızca geliştirme, Seed:DemoData=true): raporların ve panelin boş
/// görünmemesi için 4 demo mağaza, müdür/kasiyer, 12 ürün, stok, son 60 günün satışları,
/// ~%4 iade ve bu ay/geçen ay için hedefler üretir. Bir kez çalışır (CodeSequence işareti).
/// Rastgelelik sabit tohumlu: her kurulumda aynı veri. Gerçek ortamda ASLA çalışmaz.
///
/// Demo girişleri (README'de de yazar):
///   Panel müdürü : demo.{magaza-kodu}@lcwaikiki.com / Demo123!
///   Kasa         : müdür PIN 4826, kasiyer PIN 1357 (personel no: 8{kod}01 / 8{kod}02)
/// </summary>
public static class DemoDataSeeder
{
    private const string Marker = "DEMO:SEEDED";
    public const string ManagerPassword = "Demo123!";
    public const string ManagerPin = "4826";
    public const string CashierPin = "1357";
    private const int Days = 60;

    private sealed record DemoStore(string Code, string Name, string City, double DailySales, double TargetFactor);

    /// <summary>TargetFactor: hedef = gerçekleşme temposu × faktör (1'in altı: önde, üstü: geride).</summary>
    private static readonly DemoStore[] Stores =
    [
        new("101", "İstanbul Kanyon", "İstanbul", 16, 0.75),
        new("201", "Ankara Armada", "Ankara", 11, 1.00),
        new("301", "İzmir Optimum", "İzmir", 8, 1.45),
        new("501", "Antalya TerraCity", "Antalya", 6, 1.15),
    ];

    private sealed record DemoProduct(string Name, string CategoryCode, Department Dept, decimal Price, string[] Colors, string[] Sizes);

    private static readonly DemoProduct[] Products =
    [
        new("Slim Fit Chino Pantolon", "ER", Department.Men, 699.99m, ["Bej", "Lacivert"], ["30", "32", "34", "36"]),
        new("Regular Fit Oxford Gömlek", "ER", Department.Men, 549.99m, ["Beyaz", "Açık Mavi"], ["S", "M", "L", "XL"]),
        new("Oversize Kapüşonlu Sweatshirt", "ER", Department.Men, 649.99m, ["Antrasit", "Haki"], ["S", "M", "L", "XL"]),
        new("Basic Bisiklet Yaka Tişört", "ER", Department.Men, 199.99m, ["Beyaz", "Siyah"], ["S", "M", "L", "XL"]),
        new("Yüksek Bel Mom Jean", "KD", Department.Women, 749.99m, ["Açık Mavi", "Siyah"], ["34", "36", "38", "40"]),
        new("Örgü Triko Hırka", "KD", Department.Women, 599.99m, ["Ekru", "Bordo"], ["S", "M", "L"]),
        new("Midi Saten Etek", "KD", Department.Women, 499.99m, ["Siyah", "Zümrüt"], ["S", "M", "L"]),
        new("Erkek Çocuk Baskılı Tişört", "CC", Department.Boy, 179.99m, ["Lacivert", "Gri"], ["10-11", "12-13", "14-15"]),
        new("Kız Çocuk Fırfırlı Elbise", "CC", Department.Girl, 399.99m, ["Pembe", "Lila"], ["10-11", "12-13", "14-15"]),
        new("Bebek Pamuklu Tulum", "CC", Department.Baby, 249.99m, ["Krem", "Mavi"], ["3-6 Ay", "6-9 Ay", "9-12 Ay"]),
        new("Deri Görünümlü Kemer", "AK", Department.Men, 249.99m, ["Siyah", "Kahve"], ["90", "100"]),
        new("Spor Taban Sneaker", "AY", Department.Women, 1099.99m, ["Beyaz", "Siyah"], ["37", "38", "39", "40"]),
    ];

    public static async Task RunAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.PeekCodeSequenceValueAsync(Marker, ct) > 0) return;

        logger.LogInformation("Demo verisi üretiliyor (yalnızca geliştirme)...");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var rnd = new Random(2026);

        // ── Ürünler (ürün kodu ve EAN-13 barkod mevcut akışla üretilir) ──
        var categories = await db.Categories.Where(c => c.Code != null).ToDictionaryAsync(c => c.Code!, c => c.Id, ct);
        var season = BusinessClock.Today.Month is >= 9 or <= 2 ? Season.Winter : Season.Summer;
        var variantIds = new List<int>();
        foreach (var p in Products)
        {
            if (!categories.TryGetValue(p.CategoryCode, out var categoryId)) continue;
            var created = await sender.Send(new CreateProductCommand(
                p.Name, "Demo ürünü", categoryId, season, BusinessClock.Today.Year, p.Dept, null,
                p.Colors.SelectMany(c => p.Sizes.Select(s => new CreateVariantRequest(null, c, s, null, p.Price))).ToList()), ct);
            variantIds.AddRange(created.Variants.Select(v => v.Id));
        }
        var variants = await db.ProductVariants.Include(v => v.Product).Include(v => v.PriceHistories)
            .Where(v => variantIds.Contains(v.Id)).ToListAsync(ct);
        foreach (var v in variants)
            db.WarehouseStocks.Add(new WarehouseStock { VariantId = v.Id, Quantity = rnd.Next(150, 400) });

        // ── Mağazalar + müdür/kasiyer ──────────────────────────────
        var storeRows = new List<(DemoStore Demo, Store Store, string CashierId)>();
        foreach (var d in Stores)
        {
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Code == d.Code, ct);
            if (store is null)
            {
                store = new Store { Name = d.Name, Code = d.Code, City = d.City, Address = $"{d.Name} AVM", IsActive = true };
                db.Stores.Add(store);
                await db.SaveChangesAsync(ct);
            }

            await EnsureUserAsync(users, $"demo.{d.Code}@lcwaikiki.com", "Demo", $"Müdür {d.City}", "StoreManager",
                store.Id, $"8{d.Code}01", ManagerPin, ManagerPassword);
            var cashier = await EnsureUserAsync(users, $"demo.{d.Code}.kasa@lcwaikiki.com", "Demo", $"Kasiyer {d.City}", "Cashier",
                store.Id, $"8{d.Code}02", CashierPin, null);
            storeRows.Add((d, store, cashier.Id));

            foreach (var v in variants)
                db.StoreInventories.Add(new StoreInventory { StoreId = store.Id, VariantId = v.Id, Quantity = rnd.Next(60, 120) });
        }
        await db.SaveChangesAsync(ct);

        var inventories = await db.StoreInventories
            .Where(i => variantIds.Contains(i.VariantId) && storeRows.Select(r => r.Store.Id).Contains(i.StoreId))
            .ToDictionaryAsync(i => (i.StoreId, i.VariantId), ct);
        var reasons = await db.ReturnReasons.Select(r => r.Id).ToListAsync(ct);

        // ── Satışlar (son 60 gün) ──────────────────────────────────
        var today = BusinessClock.Today;
        var nowLocal = BusinessClock.ToLocal(DateTime.UtcNow);
        var timestamps = new List<(int SaleId, DateTime Utc)>();
        var soldItems = new List<(SaleItem Item, Sale Sale, DateTime Local, int StoreId, string CashierId)>();

        for (var dayOffset = Days - 1; dayOffset >= 0; dayOffset--)
        {
            var day = today.AddDays(-dayOffset);
            foreach (var (demo, store, cashierId) in storeRows)
            {
                var weekday = day.DayOfWeek switch { DayOfWeek.Saturday => 1.6, DayOfWeek.Sunday => 1.45, DayOfWeek.Friday => 1.2, _ => 1.0 };
                var trend = 0.85 + 0.3 * (Days - dayOffset) / Days;     // son haftalara doğru hafif artış
                var count = (int)Math.Round(demo.DailySales * weekday * trend * (0.75 + rnd.NextDouble() * 0.5));

                var batch = new List<(Sale Sale, DateTime Local)>();
                for (var n = 0; n < count; n++)
                {
                    var local = day.AddHours(10).AddMinutes(rnd.Next(0, 11 * 60));
                    if (local > nowLocal) continue;                      // bugünün gelecek saatleri yok

                    var sale = new Sale
                    {
                        StoreId = store.Id, CashierId = cashierId, Status = SaleStatus.Completed,
                        ReceiptNumber = $"{store.Id:000}{day:yyyyMMdd}{await db.GetNextReceiptSequenceValueAsync(store.Id, day, ct):D4}"
                    };
                    var lines = rnd.Next(100) < 55 ? 1 : rnd.Next(100) < 75 ? 2 : 3;
                    foreach (var v in variants.OrderBy(_ => rnd.Next()).Take(lines))
                    {
                        var qty = rnd.Next(100) < 88 ? 1 : 2;
                        var price = v.PriceHistories.Count > 0 ? v.PriceHistories.First().Price : Products.First(p => p.Name == v.Product.Name).Price;
                        var inv = inventories[(store.Id, v.Id)];
                        inv.Quantity -= qty;
                        var item = new SaleItem
                        {
                            VariantId = v.Id, BarcodeSnapshot = v.Barcode, ProductNameSnapshot = v.Product.Name,
                            ColorSnapshot = v.Color, SizeSnapshot = v.Size, PriceAtSale = price, Quantity = qty,
                            LineTotal = price * qty, StockQuantityAfterSale = inv.Quantity
                        };
                        sale.Items.Add(item);
                        soldItems.Add((item, sale, local, store.Id, cashierId));
                    }
                    sale.TotalAmount = sale.FinalAmount = sale.Items.Sum(i => i.LineTotal);
                    var card = rnd.Next(100) < 62;
                    sale.PaymentType = card ? PaymentType.Card : PaymentType.Cash;
                    sale.Payments.Add(new SalePayment
                    {
                        PaymentType = sale.PaymentType, Amount = sale.FinalAmount,
                        InstallmentCount = card && sale.FinalAmount > 1000 && rnd.Next(100) < 30 ? (rnd.Next(2) == 0 ? 4 : 8) : null
                    });
                    db.Sales.Add(sale);
                    batch.Add((sale, local));
                }
                await db.SaveChangesAsync(ct);
                timestamps.AddRange(batch.Select(b => (b.Sale.Id, TimeZoneInfo.ConvertTimeToUtc(b.Local, BusinessClock.TimeZone))));
            }
        }

        // ── İadeler (~%4, satıştan 1-5 gün sonra, bugünü geçmeden) ──
        var returnTimes = new List<(int ReturnId, DateTime Utc)>();
        foreach (var (item, sale, local, storeId, cashierId) in soldItems.Where(_ => rnd.Next(100) < 4))
        {
            var at = local.AddDays(rnd.Next(1, 6)).AddHours(rnd.Next(-2, 3));
            if (at > nowLocal) continue;
            var amount = item.PriceAtSale;
            var ret = new SaleReturn { StoreId = storeId, CashierId = cashierId, TotalAmount = amount, Note = "Demo iade" };
            ret.Lines.Add(new SaleReturnLine
            {
                SaleItemId = item.Id, OriginalSaleId = sale.Id, OriginalReceiptNumber = sale.ReceiptNumber,
                VariantId = item.VariantId, BarcodeSnapshot = item.BarcodeSnapshot, ProductNameSnapshot = item.ProductNameSnapshot,
                Quantity = 1, Amount = amount, ReturnReasonId = reasons.Count > 0 ? reasons[rnd.Next(reasons.Count)] : null
            });
            ret.Payouts.Add(new SaleReturnPayout
            {
                Method = sale.PaymentType == PaymentType.Card ? RefundMethod.Card : RefundMethod.Cash, Amount = amount
            });
            item.RefundedQuantity += 1;
            item.RefundedAmount += amount;
            sale.RefundedAmount += amount;
            sale.Status = sale.RefundedAmount >= sale.FinalAmount ? SaleStatus.Refunded : SaleStatus.Partial;
            inventories[(storeId, item.VariantId)].Quantity += 1;
            db.SaleReturns.Add(ret);
            await db.SaveChangesAsync(ct);
            returnTimes.Add((ret.Id, TimeZoneInfo.ConvertTimeToUtc(at, BusinessClock.TimeZone)));
        }
        await db.SaveChangesAsync(ct);

        // Kayıt anı = satış/iade anı (interceptor CreatedAt'i "şimdi" yazar; geriye çekilir)
        foreach (var (id, utc) in timestamps)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Sales SET CreatedAt = {utc}, UpdatedAt = {utc} WHERE Id = {id}", ct);
        foreach (var (id, utc) in returnTimes)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE SaleReturns SET CreatedAt = {utc}, UpdatedAt = {utc} WHERE Id = {id}", ct);

        // ── Hedefler: geçen ay ve bu ay (farklı tempolar) ──────────
        await SeedTargetsAsync(db, storeRows.Select(r => (r.Store.Id, r.Demo.TargetFactor)).ToList(), ct);

        await db.GetNextCodeSequenceValueAsync(Marker, ct);
        logger.LogInformation("Demo verisi hazır: {Stores} mağaza, {Products} ürün, {Sales} satış, {Returns} iade.",
            storeRows.Count, Products.Length, timestamps.Count, returnTimes.Count);
    }

    private static async Task SeedTargetsAsync(ApplicationDbContext db, List<(int StoreId, double Factor)> stores, CancellationToken ct)
    {
        var today = BusinessClock.Today;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        foreach (var month in new[] { thisMonth.AddMonths(-1), thisMonth })
        {
            var (fromUtc, _) = BusinessClock.DayRangeUtc(month);
            var lastDay = month == thisMonth ? today : month.AddMonths(1).AddDays(-1);
            var (_, toUtc) = BusinessClock.DayRangeUtc(lastDay);
            var elapsed = (lastDay - month).Days + 1;
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

            foreach (var (storeId, factor) in stores)
            {
                var sales = await db.Sales.Where(s => s.StoreId == storeId && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
                    .SumAsync(s => (decimal?)s.FinalAmount, ct) ?? 0;
                var refunds = await db.SaleReturns.Where(r => r.StoreId == storeId && r.CreatedAt >= fromUtc && r.CreatedAt < toUtc)
                    .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0;
                var projected = (sales - refunds) / elapsed * daysInMonth;
                var target = Math.Round(projected * (decimal)factor / 1000m, 0) * 1000m;   // yuvarlak hedef
                if (target <= 0) continue;
                if (!await db.StoreSalesTargets.AnyAsync(t => t.StoreId == storeId && t.Year == month.Year && t.Month == month.Month, ct))
                    db.StoreSalesTargets.Add(new StoreSalesTarget { StoreId = storeId, Year = month.Year, Month = month.Month, TargetAmount = target });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<AppUser> EnsureUserAsync(UserManager<AppUser> users, string email, string first, string last,
        string role, int storeId, string personelId, string pin, string? password)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new AppUser
        {
            UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true,
            StoreId = storeId, IsActive = true, PersonelId = personelId, PinHash = PinHasher.Hash(pin),
            IsFirstLogin = false   // demo: PIN zaten belirlenmiş
        };
        var result = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Demo kullanıcı oluşturulamadı: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
