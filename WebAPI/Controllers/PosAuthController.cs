using Infrastructure.Identity;
using WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/auth")]
public class PosAuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtService _jwtService;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public PosAuthController(
        UserManager<AppUser> userManager,
        JwtService jwtService,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _cache = cache;
    }

    /// <summary>
    /// POS Terminal girişi — Personel ID + PIN ile
    /// </summary>
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Pin)]
    public async Task<IActionResult> Login(
        [FromBody] PosLoginRequest request)
    {
        // ── 1. Kullanıcı + PIN (kilit korumalı, tek tip hata mesajı) ──
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PersonelId == request.PersonelId);

        switch (await PinSecurity.VerifyAsync(_userManager, user, request.Pin))
        {
            case PinCheckResult.LockedOut:
                return StatusCode(StatusCodes.Status423Locked, new { message = PinSecurity.LockedMessage(user) });
            case PinCheckResult.Invalid:
                return Unauthorized(new { message = PinSecurity.InvalidMessage });
        }

        // ── 2. Hesap durumu (sadece doğru PIN'den sonra açıklanır) ──
        if (!user!.IsActive)
            return Unauthorized(new
            {
                message = "Hesabınız aktif değil. Yöneticinizle iletişime geçin."
            });

        if (user.StoreId is null)
            return Unauthorized(new
            {
                message = "Bu personel herhangi bir mağazaya atanmamış."
            });

        // ── 3. Token üret ────────────────────────────────────
        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user, roles);

        return Ok(new PosLoginResponse(
            Token: token,
            FullName: user.FullName,
            PersonelId: user.PersonelId!,
            StoreId: user.StoreId.Value,
            IsFirstLogin: user.IsFirstLogin,
            Roles: roles
        ));
    }

    /// <summary>
    /// PIN değiştir — ilk girişte zorunlu
    /// </summary>
    [HttpPost("change-pin")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Pin)]
    [Authorize]   // Eskiden kimliksizdi: personel no'yu bilen herkes sıfırlanmış (1234) PIN'i değiştirip hesabı ele geçirebiliyordu
    public async Task<IActionResult> ChangePin(
        [FromBody] ChangePinRequest request)
    {
        // Sadece oturum sahibi KENDİ PIN'ini değiştirebilir
        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.PersonelId != request.PersonelId)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Sadece kendi PIN'inizi değiştirebilirsiniz." });

        // Eski PIN (kilit korumalı)
        switch (await PinSecurity.VerifyAsync(_userManager, user, request.OldPin))
        {
            case PinCheckResult.LockedOut:
                return StatusCode(StatusCodes.Status423Locked, new { message = PinSecurity.LockedMessage(user) });
            case PinCheckResult.Invalid:
                return BadRequest(new { message = "Mevcut PIN hatalı." });
        }

        var pinError = PinSecurity.ValidateNewPin(request.NewPin);
        if (pinError is not null)
            return BadRequest(new { message = pinError });

        if (request.NewPin == request.OldPin)
            return BadRequest(new { message = "Yeni PIN eski PIN ile aynı olamaz." });

        user.PinHash = HashPin(request.NewPin);
        user.IsFirstLogin = false;
        await _userManager.UpdateAsync(user);

        // Zorunlu değişim sonrası tam yetkili yeni token (eski token "mustChangePin" taşır)
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            message = "PIN başarıyla güncellendi.",
            token = _jwtService.GenerateToken(user, roles)
        });
    }

    // ── PIN hash / doğrulama ─────────────────────────────────
    // Tek nokta: Infrastructure.Identity.PinHasher (PBKDF2; eski SHA256
    // formatlarını da okur ve başarılı girişte yükseltir - bkz. PinSecurity).
    public static string HashPin(string pin) => PinHasher.Hash(pin);

    public static bool VerifyPin(string pin, string? pinHash) => PinHasher.Verify(pin, pinHash, out _);

    // ── PIN Sıfırla (Yönetici) ───────────────────────────────────
    [HttpPost("reset-pin/{userId}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> ResetPin(
        string userId,
        CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { message = "Kullanıcı bulunamadı." });

        // Mağaza müdürü SADECE kendi mağazasının kasiyerlerinin PIN'ini sıfırlayabilir
        if (!User.IsInRole("Admin"))
        {
            var manager = await _userManager.GetUserAsync(User);
            var targetRoles = await _userManager.GetRolesAsync(user);
            var allowed = manager?.StoreId is not null
                          && user.StoreId == manager.StoreId
                          && targetRoles.Count == 1
                          && targetRoles[0] == "Cashier";
            if (!allowed)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Sadece kendi mağazanızın kasiyerlerinin PIN'ini sıfırlayabilirsiniz." });
        }

        // PIN'i 1234'e sıfırla (giriş ve onayla AYNI hash formatı)
        user.PinHash = HashPin("1234");
        user.IsFirstLogin = true;  // İlk girişte PIN değiştirmeye zorla (token "mustChangePin" taşır)

        // Hatalı denemelerden kilitlenmiş kasiyerin kilidi de açılır
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;

        // Güvenlik damgasını yenile (kaydeder): eski PIN'le açılmış oturumlar düşer
        await _userManager.UpdateSecurityStampAsync(user);
        UserTokenValidation.Invalidate(_cache, user.Id);

        return Ok(new { message = "PIN 1234 olarak sıfırlandı. Kullanıcı ilk girişte değiştirmek zorunda." });
    }
}

// ── Request / Response ───────────────────────────────────────
public record PosLoginRequest(
    string PersonelId,
    string Pin
);

public record PosLoginResponse(
    string Token,
    string FullName,
    string PersonelId,
    int StoreId,
    bool IsFirstLogin,
    IList<string> Roles
);

public record ChangePinRequest(
    string PersonelId,
    string OldPin,
    string NewPin
);