using System.Threading.RateLimiting;
using Infrastructure.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Common;

public static class RateLimitPolicies
{
    /// <summary>PIN/şifre denenen uçlar: IP başına dakikada 20 istek.</summary>
    public const string Pin = "pin";

    public static IServiceCollection AddPinRateLimiting(this IServiceCollection services)
        => services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, ct) =>
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Çok fazla deneme. Lütfen bir dakika sonra tekrar deneyin." }, ct);

            // Bölümleme: eskiden TÜM istekler sadece IP'ye göre (20/dk) sınırlanıyordu.
            // Mağazada bütün kasalar NAT arkasında tek IP'den çıktığı için yoğun saatte
            // yönetici onayları / girişler tüm mağaza için 429 ile reddediliyordu.
            //  • Kimlikli istek (yönetici onayı, PIN değiştirme): KULLANICI başına 20/dk
            //    - bir kasiyer başka kasaların kotasını tüketemez.
            //  • Anonim istek (giriş): IP başına 120/dk - mağaza ölçeğinde yeterli; hedefli
            //    deneme-yanılmaya karşı asıl koruma hesap kilidi (5 hata → 15 dk).
            options.AddPolicy(Pin, http =>
            {
                // Sınırlar yapılandırılabilir (RateLimiting:PerUserPerMinute / PerIpPerMinute);
                // entegrasyon testleri yüksek değer verir, varsayılanlar üretim içindir.
                var config = http.RequestServices.GetRequiredService<IConfiguration>();
                var perUser = config.GetValue("RateLimiting:PerUserPerMinute", 20);
                var perIp = config.GetValue("RateLimiting:PerIpPerMinute", 120);

                var userId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                return userId is not null
                    ? RateLimitPartition.GetFixedWindowLimiter("user:" + userId, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = perUser,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })
                    : RateLimitPartition.GetFixedWindowLimiter(
                        "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = perIp,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
            });
        });
}

/// <summary>
/// PIN'i hiç değiştirilmemiş hesabın (ilk giriş / sıfırlanmış PIN, varsayılan
/// 1234) token'ı "mustChangePin" taşır ve SADECE PIN değiştirme ucunu
/// kullanabilir. Eskiden kasiyerler PIN'i hiç değiştirmiyordu - herkes
/// 1234 ile başkasının hesabına girip satış/iade yapabiliyordu.
/// </summary>
public class MustChangePinMiddleware
{
    private static readonly string[] AllowedPaths =
    {
        "/api/pos/auth/change-pin",
        "/api/pos/auth/login"
    };

    private readonly RequestDelegate _next;
    public MustChangePinMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(JwtService.MustChangePinClaim, "true")
            && !AllowedPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "PIN'inizi değiştirmeniz gerekiyor. Lütfen önce yeni PIN belirleyin.",
                mustChangePin = true
            });
            return;
        }

        await _next(context);
    }
}
