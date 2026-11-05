using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCWaikiki.POS.IntegrationTests.Infrastructure;

/// <summary>
/// Gerçek WebAPI'yi (tüm middleware, kimlik doğrulama, MediatR pipeline, EF Core)
/// bellek içinde çalıştırır. Her factory kendi BENZERSİZ SQL Server veritabanını
/// oluşturur (startup migration + seed) ve test bitince siler.
///
/// SQL Server gerçek olmalıdır: kod MERGE, rowversion ve filtreli unique index
/// kullanır - SQLite / InMemory sağlayıcısı bu davranışları test edemez.
///
/// Sunucu: POS_TEST_SQL ortam değişkeni (varsayılan LocalDB). CI'da örn.
///   POS_TEST_SQL="localhost,1433;User Id=sa;Password=..."
/// </summary>
public class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = $"LCWaikikiPOS_Test_{Guid.NewGuid():N}";

    /// <summary>Kullanıcı başına PIN/onay istek sınırı (testlerde pratikte sınırsız).</summary>
    protected virtual int PerUserRateLimit => 100_000;
    protected virtual int PerIpRateLimit => 100_000;

    public string ConnectionString
    {
        get
        {
            var server = Environment.GetEnvironmentVariable("POS_TEST_SQL") ?? @"(localdb)\mssqllocaldb";
            var auth = server.Contains("User Id", StringComparison.OrdinalIgnoreCase) ? "" : "Trusted_Connection=True;";
            return $"Server={server};Database={DatabaseName};{auth}MultipleActiveResultSets=true;TrustServerCertificate=True";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: geliştirme JWT anahtarı + seed (admin, test kasiyeri, kategoriler)
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("RateLimiting:PerUserPerMinute", PerUserRateLimit.ToString());
        builder.UseSetting("RateLimiting:PerIpPerMinute", PerIpRateLimit.ToString());
    }

    public Task InitializeAsync()
    {
        // Sunucuyu başlat: migration + seed burada çalışır
        _ = Server;
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
        }
        finally
        {
            await base.DisposeAsync();
        }
    }

    /// <summary>Testin veritabanını doğrudan sorgulamak için (doğrulama amaçlı).</summary>
    public async Task<T> QueryDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await query(db);
    }
}

/// <summary>Tüm API testleri TEK sunucu + veritabanını paylaşır (migration bir kez çalışır).
/// Her test kendi mağazasını/kullanıcılarını oluşturduğu için birbirini etkilemez.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<PosApiFactory>
{
    public const string Name = "api";
}
