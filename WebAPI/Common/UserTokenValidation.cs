using System.Security.Claims;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace WebAPI.Common;

/// <summary>
/// Her kimlikli istekte JWT'nin HÂLÂ geçerli bir kullanıcıya ait olduğunu doğrular.
///
/// Eskiden token sadece imza + süre ile doğrulanıyordu: pasife alınan
/// (işten çıkan) kasiyerin 8 saatlik token'ı çalışmaya devam ediyor, satış ve
/// iade yapabiliyordu. Artık:
///  • Kullanıcı pasifse ya da silinmişse token reddedilir.
///  • Token'daki güvenlik damgası (sstamp) kullanıcınınkiyle eşleşmeli.
///    Pasife alma ve PIN sıfırlama damgayı yeniler → eski oturumlar düşer.
///  • Yönetici ONAY token'ları (tokenType=approval) API'ye giriş için
///    kullanılamaz (aynı anahtarla imzalandıkları için eskiden geçiyordu).
///
/// Veritabanı yükünü azaltmak için kullanıcı durumu 30 sn önbelleklenir;
/// pasife alma / PIN sıfırlama önbelleği hemen temizler (Invalidate).
/// </summary>
public static class UserTokenValidation
{
    public const string SecurityStampClaim = JwtService.SecurityStampClaim;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private sealed record UserState(bool IsActive, string? SecurityStamp);

    private static string CacheKey(string userId) => $"auth-user-state:{userId}";

    public static void Invalidate(IMemoryCache cache, string userId) => cache.Remove(CacheKey(userId));

    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null) { context.Fail("Kimlik bulunamadı."); return; }

        if (principal.HasClaim("tokenType", "approval"))
        {
            context.Fail("Onay token'ı oturum token'ı olarak kullanılamaz.");
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) { context.Fail("Geçersiz token."); return; }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();

        var state = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(userId);
            return user is null ? null : new UserState(user.IsActive, user.SecurityStamp);
        });

        if (state is null || !state.IsActive)
        {
            context.Fail("Kullanıcı pasif veya bulunamadı.");
            return;
        }

        if ((principal.FindFirstValue(SecurityStampClaim) ?? string.Empty) != (state.SecurityStamp ?? string.Empty))
            context.Fail("Oturum geçersiz kılınmış. Lütfen tekrar giriş yapın.");
    }
}
