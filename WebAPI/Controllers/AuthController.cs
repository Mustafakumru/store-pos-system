using Application.Common.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtService _jwtService;
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        JwtService jwtService,
        IApplicationDbContext context,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _context = context;
        _cache = cache;
    }
    // ── Public: Login ────────────────────────────────────────────
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(WebAPI.Common.RateLimitPolicies.Pin)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Geçersiz email veya şifre." });

        // lockoutOnFailure: true → 5 hatalı şifre = 15 dk kilit (eskiden false: sınırsız deneme)
        var result = await _signInManager
            .CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Çok fazla hatalı deneme. Hesap geçici olarak kilitlendi, 15 dakika sonra tekrar deneyin."
            });

        if (!result.Succeeded)
            return Unauthorized(new { message = "Geçersiz email veya şifre." });

        // Pasif bilgisi sadece doğru şifreden sonra (hesap varlığını sızdırmasın)
        if (!user.IsActive)
            return Unauthorized(new { message = "Bu kullanıcı pasif durumda." });

        var roles = await _userManager.GetRolesAsync(user);
        // Panel girişi şifreyle yapıldı: varsayılan POS PIN'i paneli kilitlemez
        var token = _jwtService.GenerateToken(user, roles, enforcePinChange: false);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles,
            StoreId = user.StoreId   // ✅ Frontend mağazayı bilsin
        });
    }

    // ── Admin Only: Kullanıcı oluştur ───────────────────────────
    // Kasiyer ve mağaza müdürü bu endpoint ile eklenir
    [HttpPost("create-user")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> CreateUser(
      [FromBody] CreateUserRequest request)
    {
        // Mağaza müdürü SADECE kendi mağazasına kasiyer ekleyebilir
        // (rol ve mağaza istekten değil, müdürün kendi hesabından gelir).
        if (!User.IsInRole("Admin"))
        {
            var manager = await _userManager.GetUserAsync(User);
            if (manager?.StoreId is null)
                return Forbid();

            request.Role = "Cashier";
            request.StoreId = manager.StoreId;
        }

        var validRoles = new[] { "Admin", "StoreManager", "Cashier", "User" };
        if (!validRoles.Contains(request.Role))
            return BadRequest(new { message = "Geçersiz rol." });

        // Ad / soyad (eskiden hiç doğrulanmıyordu: boş adla kullanıcı açılabiliyordu)
        request.FirstName = request.FirstName?.Trim() ?? string.Empty;
        request.LastName = request.LastName?.Trim() ?? string.Empty;
        if (!Application.Common.Validation.CommonRules.IsPersonName(request.FirstName)
            || !Application.Common.Validation.CommonRules.IsPersonName(request.LastName))
            return BadRequest(new { message = "Ad ve soyad zorunludur ve sadece harf içerebilir." });
        if (request.FirstName.Length > 100 || request.LastName.Length > 100)
            return BadRequest(new { message = "Ad ve soyad en fazla 100 karakter olabilir." });
        request.Email = request.Email?.Trim() ?? string.Empty;

        // Kasiyerler POS'a PersonelId + PIN ile girer; web paneli şifresi
        // yalnızca panele giriş yapacak roller için zorunludur.
        var needsPassword = request.Role != "Cashier";
        if (needsPassword && string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Bu rol için şifre zorunludur." });

        // Kasiyer ve müdür için StoreId zorunlu
        if (request.Role is "Cashier" or "StoreManager" && request.StoreId is null)
            return BadRequest(new
            {
                message = "Kasiyer ve mağaza müdürü için StoreId zorunludur."
            });

        // ✅ StoreId varsa mağazanın gerçekten var olup olmadığını kontrol et
        if (request.StoreId.HasValue)
        {
            var storeExists = await _context.Stores
                .AnyAsync(s => s.Id == request.StoreId.Value);

            if (!storeExists)
                return BadRequest(new
                {
                    message = $"ID={request.StoreId} mağaza bulunamadı."
                });
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true,
            StoreId = request.StoreId,
            IsActive = true,

            // ✅ Kasiyer/StoreManager ise PersonelId ve PIN üret
            PersonelId = request.Role is "Cashier" or "StoreManager"
                            ? await GenerateUniquePersonelIdAsync(HttpContext.RequestAborted)
                            : null,

            PinHash = request.Role is "Cashier" or "StoreManager"
                            ? PosAuthController.HashPin("1234")
                            : null,

            IsFirstLogin = true
        };

        // ── Kullanıcıyı kaydet ───────────────────────────────────────
        var createResult = string.IsNullOrWhiteSpace(request.Password)
            ? await _userManager.CreateAsync(user)           // kasiyer: şifresiz (sadece PIN)
            : await _userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
            return BadRequest(new
            {
                message = "Kullanıcı oluşturulamadı.",
                errors = createResult.Errors.Select(e => e.Description)
            });

        // ── Role ata ─────────────────────────────────────────────────
        await _userManager.AddToRoleAsync(user, request.Role);

        // Response'a PersonelId ekle:
        return Ok(new
        {
            message = $"{request.Role} başarıyla oluşturuldu.",
            userId = user.Id,
            fullName = user.FullName,
            role = request.Role,
            storeId = user.StoreId,
            personelId = user.PersonelId
        });
    }
    /// <summary>
    /// Personel numarası: {yyMM}{sıra:000} (örn. 2611001).
    ///
    /// Eskiden "bu ay kaç kişi var + 1" sayılıyordu: aynı anda oluşturulan
    /// kullanıcılar AYNI numarayı alıyordu (testte 6 kullanıcıya 2611015) ve
    /// POS girişi hangi hesaba gireceğini bilemiyordu. Artık sıra, atomik
    /// veritabanı sayacından (CodeSequences, MERGE + HOLDLOCK) alınır; her çağrı
    /// farklı değer döner. Sayaç yeni eklendiğinde mevcut numaralarla
    /// çakışırsa sıradakine geçilir. Son savunma: PersonelId unique index.
    /// </summary>
    private async Task<string> GenerateUniquePersonelIdAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var prefix = $"{now:yy}{now:MM}"; // Örn: "2503" → 2025 Mart

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var sequence = await _context.GetNextCodeSequenceValueAsync($"personel-{prefix}", ct);
            var personelId = $"{prefix}{sequence:D3}";

            if (!await _userManager.Users.AnyAsync(u => u.PersonelId == personelId, ct))
                return personelId;
        }

        throw new InvalidOperationException("Personel numarası üretilemedi. Lütfen tekrar deneyin.");
    }

    // ── Admin Only: Kullanıcıları listele ───────────────────────
    [HttpGet("users")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetUsers()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");

        var usersQuery = _userManager.Users.AsQueryable();

        // StoreManager sadece kendi mağazasının kullanıcılarını görür.
        // Mağazası atanmamış müdür eskiden filtre uygulanmadığı için TÜM
        // kullanıcıları görüyordu - artık reddedilir.
        if (!isAdmin)
        {
            if (currentUser?.StoreId is null)
                return Forbid();
            usersQuery = usersQuery.Where(u => u.StoreId == currentUser.StoreId);
        }

        var users = await usersQuery
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PersonelId,
                u.IsActive,
                u.IsFirstLogin,
                u.StoreId
            })
            .ToListAsync();

        // Rolleri ekle
        var result = new List<object>();
        foreach (var u in users)
        {
            var appUser = await _userManager.FindByIdAsync(u.Id);
            var roles = await _userManager.GetRolesAsync(appUser!);
            result.Add(new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.PersonelId,
                u.IsActive,
                u.IsFirstLogin,
                u.StoreId,
                Role = roles.FirstOrDefault(),
                Roles = roles
            });
        }

        return Ok(result);
    }
    // ── Kullanıcı Aktif/Pasif Toggle ─────────────────────────────
    [HttpPatch("users/{userId}/toggle-status")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> ToggleUserStatus(
        string userId,
        CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { message = "Kullanıcı bulunamadı." });

        var forbidden = await CheckCanManageUserAsync(user);
        if (forbidden is not null) return forbidden;

        if (user.Id == _userManager.GetUserId(User))
            return BadRequest(new { message = "Kendi hesabınızı pasife alamazsınız." });

        user.IsActive = !user.IsActive;

        // Güvenlik damgasını yenile (kullanıcıyı da kaydeder): pasife alınan
        // kullanıcının elindeki token'lar bir sonraki istekte reddedilir.
        // Eskiden token 8 saat boyunca geçerli kalıyordu.
        await _userManager.UpdateSecurityStampAsync(user);
        WebAPI.Common.UserTokenValidation.Invalidate(_cache, user.Id);

        return Ok(new
        {
            userId = user.Id,
            isActive = user.IsActive,
            message = user.IsActive ? "Kullanıcı aktive edildi." : "Kullanıcı pasife alındı."
        });
    }

    // ── Kullanıcı Bilgisi Güncelle (ad, soyad, e-posta) ─────────
    // Rol, mağaza, PersonelId ve PIN bu uçtan DEĞİŞMEZ.
    [HttpPut("users/{userId}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> UpdateUser(
        string userId,
        [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { message = "Kullanıcı bulunamadı." });

        var forbidden = await CheckCanManageUserAsync(user);
        if (forbidden is not null) return forbidden;

        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email))
            return BadRequest(new { message = "Ad, soyad ve e-posta zorunludur." });
        if (firstName.Length > 100 || lastName.Length > 100)
            return BadRequest(new { message = "Ad ve soyad en fazla 100 karakter olabilir." });
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

        user.FirstName = firstName;
        user.LastName = lastName;

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _userManager.FindByEmailAsync(email);
            if (taken is not null && taken.Id != user.Id)
                return BadRequest(new { message = "Bu e-posta adresi başka bir kullanıcıda kayıtlı." });

            // Kullanıcı adı e-postadır (create-user ile aynı kural)
            user.Email = email;
            user.UserName = email;
            user.EmailConfirmed = true;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new
            {
                message = "Kullanıcı güncellenemedi.",
                errors = result.Errors.Select(e => e.Description)
            });

        return Ok(new
        {
            userId = user.Id,
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            message = "Kullanıcı bilgileri güncellendi."
        });
    }

    /// <summary>
    /// Admin herkesi yönetebilir. Mağaza müdürü yalnızca KENDİ mağazasındaki
    /// kasiyerleri yönetebilir (başka mağaza, admin veya müdür hesapları değil).
    /// Yetkisizse 403 sonucu, yetkiliyse null döner.
    /// </summary>
    internal async Task<IActionResult?> CheckCanManageUserAsync(AppUser target)
    {
        if (User.IsInRole("Admin")) return null;

        var manager = await _userManager.GetUserAsync(User);
        var targetRoles = await _userManager.GetRolesAsync(target);

        var allowed = manager?.StoreId is not null
                      && target.StoreId == manager.StoreId
                      && targetRoles.Count == 1
                      && targetRoles[0] == "Cashier";

        return allowed
            ? null
            : StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Sadece kendi mağazanızın kasiyerlerini yönetebilirsiniz." });
    }

    // NOT: Burada yanlış yere konmuş bir "GET api/auth/{id}" (mağaza getir) ucu
    // vardı; mağaza müdürüne her mağazanın bilgisini açıyordu. Kaldırıldı -
    // mağaza bilgisi için StoresController.GetById (kapsam kontrollü) kullanılır.
}

// ── Request / Response modelleri ────────────────────────────────
public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class CreateUserRequest
{
    public string Email { get; set; } = null!;
    public string? Password { get; set; }          // Cashier için opsiyonel (POS'a PIN ile girer)
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;  // Cashier, StoreManager vb.
    public int? StoreId { get; set; }            // Cashier/StoreManager için zorunlu
}

public record UpdateUserRequest(string? FirstName, string? LastName, string? Email);

public class AuthResponse
{
    public string Token { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public IList<string> Roles { get; init; } = [];
    public int? StoreId { get; init; }       // ✅ eklendi
}