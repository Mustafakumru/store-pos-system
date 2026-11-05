using System.Security.Cryptography;
using System.Text;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Common;

/// <summary>
/// Açılışta yapılandırma ve veritabanı kontrolleri. Hatalı yapılandırmayla API'nin
/// "çalışıyor gibi" açılması yerine anlaşılır bir mesajla hemen durması sağlanır.
/// </summary>
public static class StartupGuards
{
    /// <summary>
    /// Depoda (appsettings.Development.json) açıkça duran geliştirme anahtarlarının
    /// SHA-256 özetleri. Bu anahtarlarla imzalanan token'ı depoya erişen herkes
    /// üretebilir; geliştirme dışında kullanılırlarsa API açılmaz.
    /// </summary>
    private static readonly HashSet<string> KnownPublicKeyHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "e1221528a5e18a745f97ab79a0120b577126a47f4653186916c4a9128c160cef"
    };

    public static byte[] ValidateJwtSecret(string? secretKeyText, IHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(secretKeyText) || Encoding.UTF8.GetByteCount(secretKeyText) < 32)
            throw new InvalidOperationException(
                "JwtSettings:SecretKey tanımlı değil ya da 32 bayttan kısa. Geliştirmede " +
                "appsettings.Development.json veya 'dotnet user-secrets', production'da " +
                "JwtSettings__SecretKey ortam değişkeni ile verin.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secretKeyText)));
        if (!env.IsDevelopment() && KnownPublicKeyHashes.Contains(hash))
            throw new InvalidOperationException(
                "JwtSettings:SecretKey, depoda açıkça duran GELİŞTİRME anahtarı. Bu anahtarla " +
                $"'{env.EnvironmentName}' ortamında çalışılamaz: herkes geçerli token üretebilir. " +
                "Rastgele en az 32 baytlık yeni bir anahtar verin (örn. PowerShell: " +
                "[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }) -as [byte[]])).");

        return Encoding.UTF8.GetBytes(secretKeyText);
    }

    public static string[] ValidateAllowedOrigins(IConfiguration config, IHostEnvironment env)
    {
        var origins = config.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        origins = origins.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
        if (origins.Length == 0)
            throw new InvalidOperationException(
                "AllowedOrigins tanımlı değil: yönetim panelinin adresini (örn. https://panel.ornek.com) verin.");
        if (!env.IsDevelopment() && origins.Any(o => o == "*" || o.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "AllowedOrigins production'da '*' ya da http:// adres içeremez (yalnızca https panel adresi).");
        return origins;
    }

    /// <summary>
    /// Migration politikası (Database:AutoMigrate):
    ///  • Geliştirmede varsayılan AÇIK — bekleyenler otomatik uygulanır.
    ///  • Production'da varsayılan KAPALI — şema değişikliği bilinçli, yedek alınarak
    ///    ve tek noktadan (migration bundle / dotnet ef) yapılır. Bekleyen migration
    ///    varsa API eski şemayla çalışıp veri bozmak yerine açılmaz.
    /// </summary>
    public static async Task ApplyMigrationPolicyAsync(ApplicationDbContext db, IConfiguration config,
        IHostEnvironment env, ILogger logger)
    {
        var autoMigrate = config.GetValue<bool?>("Database:AutoMigrate") ?? env.IsDevelopment();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Veritabanı şeması güncel.");
            return;
        }

        if (autoMigrate)
        {
            logger.LogWarning("{Count} bekleyen migration uygulanıyor: {Names}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            return;
        }

        throw new InvalidOperationException(
            $"Veritabanında uygulanmamış {pending.Count} migration var: {string.Join(", ", pending)}. " +
            "Yedek aldıktan sonra 'dotnet ef database update --project Infrastructure --startup-project WebAPI' " +
            "ya da migration bundle ile uygulayın; veya bilerek Database__AutoMigrate=true verin.");
    }
}
