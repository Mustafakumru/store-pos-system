using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
namespace Infrastructure.Identity;

public static class IdentitySeeder
{
    /// <param name="isDevelopment">
    /// true: test mağazası + test kasiyeri + bilinen admin şifresi (yerel geliştirme).
    /// false (production): SADECE roller, kategoriler ve - hiç admin yoksa -
    /// <paramref name="adminPassword"/> ile admin. Eskiden "Admin123!" şifreli admin
    /// ve PIN'i 1234 olan (değiştirme zorunluluğu da olmayan) test kasiyeri her
    /// ortamda oluşturuluyordu.
    /// </param>
    /// <param name="adminPassword">Production ilk admin şifresi (yapılandırma: Seed:AdminPassword).</param>
    public static async Task SeedAsync(IServiceProvider serviceProvider, bool isDevelopment, string? adminPassword)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        // ── 1. Roller ────────────────────────────────────────────
        string[] roles = ["Admin", "User", "Cashier", "StoreManager"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── 2. Admin kullanıcısı ─────────────────────────────────
        const string adminEmail = "admin@lcwaikiki.com";
        if (isDevelopment && string.IsNullOrWhiteSpace(adminPassword))
            adminPassword = "Admin123!";   // sadece yerel geliştirme

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null && string.IsNullOrWhiteSpace(adminPassword))
        {
            Console.WriteLine("⚠️ Admin kullanıcısı yok ve Seed:AdminPassword tanımlı değil - admin oluşturulmadı.");
        }
        else if (adminUser is null)
        {
            adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "LC",
                LastName = "Admin",
                EmailConfirmed = true,
                StoreId = null   // Admin mağazaya bağlı değil
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword!);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("✅ Admin kullanıcısı oluşturuldu.");
            }
            else
                Console.WriteLine("⚠️ Admin oluşturulamadı: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Test verisi (mağaza + kasiyer) SADECE geliştirme ortamında
        if (isDevelopment)
            await SeedDevelopmentDataAsync(context, userManager);

        await SeedCategoriesAsync(context);
    }

    private static async Task SeedDevelopmentDataAsync(ApplicationDbContext context, UserManager<AppUser> userManager)
    {

        // ── 3. Test mağazası ─────────────────────────────────────
        var testStore = await context.Stores
            .FirstOrDefaultAsync(s => s.Code == "LCW-TEST-001");

        if (testStore is null)
        {
            testStore = new Store
            {
                Name = "LCW Test Mağazası",
                Code = "LCW-TEST-001",
                City = "İstanbul",
                Address = "Test Caddesi No:1",
                Phone = "02121234567",
                IsActive = true
            };
            context.Stores.Add(testStore);
            await context.SaveChangesAsync();
            Console.WriteLine("✅ Test mağazası oluşturuldu.");
        }

        // ── 4. Test kasiyeri ─────────────────────────────────────
        const string cashierEmail = "kasiyer@lcwaikiki.com";
        const string cashierPassword = "Kasiyer123!";


        var cashierUser = await userManager.FindByEmailAsync(cashierEmail);
        if (cashierUser is null)
        {
            cashierUser = new AppUser
            {
                UserName = cashierEmail,
                Email = cashierEmail,
                FirstName = "Test",
                LastName = "Kasiyer",
                EmailConfirmed = true,
                StoreId = testStore.Id,
                PersonelId = "1000001",
                PinHash = PinHasher.Hash("1234"),
                // Varsayılan PIN ile oluşturulan HER hesap ilk girişte PIN değiştirir
                // (eskiden test kasiyeri false idi: 1234 kalıcı olarak geçerliydi)
                IsFirstLogin = true
            };

            var result = await userManager.CreateAsync(cashierUser, cashierPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(cashierUser, "Cashier");

            Console.WriteLine("✅ Test kasiyeri oluşturuldu.");
        }
        else if (cashierUser.PinHash is null)
        {
            cashierUser.PinHash = PinHasher.Hash("1234");
            cashierUser.PersonelId ??= "1000001";
            cashierUser.IsFirstLogin = true;
            await userManager.UpdateAsync(cashierUser);
            Console.WriteLine("✅ Test kasiyer PIN set edildi.");
        }
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext context)
    {
        // ── 5. Kategoriler ───────────────────────────────────────
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Kadın Giyim",   Description = "Kadın giyim ürünleri"  },
                new() { Name = "Erkek Giyim",   Description = "Erkek giyim ürünleri"  },
                new() { Name = "Çocuk Giyim",   Description = "Çocuk giyim ürünleri"  },
                new() { Name = "Aksesuar",      Description = "Aksesuar ürünleri"      },
                new() { Name = "İç Giyim",      Description = "İç giyim ürünleri"     },
                new() { Name = "Spor Giyim",    Description = "Spor giyim ürünleri"   },
                new() { Name = "Ayakkabı",      Description = "Ayakkabı ürünleri"     },
                new() { Name = "Çanta & Valiz", Description = "Çanta ve valiz ürünleri"},
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ {categories.Count} kategori eklendi.");
        }
    }
}