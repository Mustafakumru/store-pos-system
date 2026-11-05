using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

public class JwtService
{
    /// <summary>
    /// Yonetici onay token'inin gecerlilik suresi. Onay, indirim uygulanirken
    /// alinir ama satis odeme bittikten sonra gonderilir (kart cekimi vb.
    /// dakikalar surebilir) - 60 sn bu akis icin yetersizdi.
    /// </summary>
    public const int ApprovalTokenLifetimeSeconds = 600;

    /// <summary>PIN değiştirmesi zorunlu hesabın token'ındaki işaret.</summary>
    public const string MustChangePinClaim = "mustChangePin";

    /// <summary>
    /// Kullanıcının güvenlik damgası. Pasife alma / PIN sıfırlama damgayı
    /// yeniler; damgası eşleşmeyen eski token'lar reddedilir (WebAPI UserTokenValidation).
    /// </summary>
    public const string SecurityStampClaim = "sstamp";

    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
        => _configuration = configuration;

    /// <summary>Panel token'ında: POS PIN'i hâlâ varsayılan (bilgi amaçlı, engellemez).</summary>
    public const string PinIsDefaultClaim = "pinIsDefault";

    /// <param name="enforcePinChange">
    /// true (POS girişi): PIN'i varsayılan hesabın token'ı SADECE PIN değiştirmeye yarar.
    /// false (web paneli, e-posta + şifre ile giriş): varsayılan PIN bir risk değildir
    /// (giriş şifreyle yapıldı) - token tam yetkilidir, panel sadece uyarı gösterir.
    /// Eskiden panel token'ı da kısıtlıydı ve panelde PIN değiştirme ekranı yoktu:
    /// yeni müdür, POS uygulamasına girip PIN değiştirmeden panelde hiçbir şey göremiyordu.
    /// </param>
    public string GenerateToken(AppUser user, IList<string> roles, bool enforcePinChange = true)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,   user.Id),
            new(ClaimTypes.Email,            user.Email!),
            new(ClaimTypes.Name,             user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SecurityStampClaim,          user.SecurityStamp ?? string.Empty)
        };

        // Roller
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // ── Mağaza claim'leri ────────────────────────────────────
        // Kasiyer ve mağaza müdürü için StoreId token'a yazılır
        // Admin için null — POS bu claim'den mağazayı tanır
        if (user.StoreId.HasValue)
        {
            claims.Add(new Claim("storeId", user.StoreId.Value.ToString()));
        }

        // PIN'i hiç değiştirilmemiş (ilk giriş / sıfırlanmış, varsayılan 1234)
        // hesabın token'ı SADECE PIN değiştirmeye yarar - bkz. MustChangePinMiddleware.
        var pinIsDefault = user.IsFirstLogin && user.PinHash is not null;
        if (pinIsDefault)
            claims.Add(enforcePinChange
                ? new Claim(MustChangePinClaim, "true")
                : new Claim(PinIsDefaultClaim, "true"));

        if (!string.IsNullOrEmpty(user.PersonelId))
            claims.Add(new Claim("personelId", user.PersonelId));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                                    double.Parse(jwtSettings["ExpiryMinutes"]!)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    /// <summary>
    /// Yönetici onayı sonrası, sadece belirtilen işlem (action) için
    /// geçerli, 60 saniyelik kısa ömürlü onay token'ı üretir.
    /// </summary>
    /// <param name="requesterUserId">Onayı isteyen kasiyer - token SADECE onun
    /// işleminde geçerlidir (başka kasada kullanılamaz).</param>
    public string GenerateApprovalToken(
        string approverUserId, string approverName, string action,
        string requesterUserId)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        var claims = new List<Claim>
    {
        new("approverId", approverUserId),
        new("approverName", approverName),
        new("approvalAction", action),
        new("requesterId", requesterUserId),
        new("tokenType", "approval"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(ApprovalTokenLifetimeSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Onay token'ını doğrular. Token süresi dolmuşsa, imzası geçersizse
    /// veya "action" eşleşmiyorsa null döner.
    /// </summary>
    public ClaimsPrincipal? ValidateApprovalToken(string token, string expectedAction)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var tokenType = principal.FindFirst("tokenType")?.Value;
            var action = principal.FindFirst("approvalAction")?.Value;

            if (tokenType != "approval" || action != expectedAction)
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}