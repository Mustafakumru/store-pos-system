using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/manager-approval")]
[Authorize] // Herhangi bir POS kullanıcısı (kasiyer) isteyebilir
[EnableRateLimiting(RateLimitPolicies.Pin)]
public class ManagerApprovalController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtService _jwtService;

    public ManagerApprovalController(
        UserManager<AppUser> userManager, JwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Kasiyer ekranında indirim/iptal/fişsiz iade vb. istendiğinde yönetici
    /// PersonelId + PIN ile onay verir. "action" işleme özel anahtardır
    /// ("discount", "return-no-receipt", ...).
    ///
    /// GÜVENLİK:
    ///  • PIN kilit korumalı (5 hatalı → 15 dk) + IP başına istek sınırı
    ///  • Onaylayan yönetici, onayı isteyen kasiyerle AYNI mağazada olmalı (Admin hariç)
    ///  • PIN'i hiç değiştirilmemiş (varsayılan PIN'li) yönetici onay veremez
    ///  • Token isteyen kasiyere bağlıdır - başka kasada kullanılamaz
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RequestApproval(
        [FromBody] ManagerApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Action) || request.Action.Length > 50)
            return BadRequest(new { message = "Geçersiz onay işlemi." });

        var requester = await _userManager.GetUserAsync(User);
        if (requester is null)
            return Unauthorized(new { message = "Oturum geçersiz." });

        var manager = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PersonelId == request.PersonelId);

        switch (await PinSecurity.VerifyAsync(_userManager, manager, request.Pin))
        {
            case PinCheckResult.LockedOut:
                return StatusCode(StatusCodes.Status423Locked, new { message = PinSecurity.LockedMessage(manager) });
            case PinCheckResult.Invalid:
                return Unauthorized(new { message = PinSecurity.InvalidMessage });
        }

        // PIN doğru - artık yetki detayları açıklanabilir
        if (!manager!.IsActive)
            return Unauthorized(new { message = "Bu yönetici hesabı pasif durumda." });

        var roles = await _userManager.GetRolesAsync(manager);
        var isAdmin = roles.Contains("Admin");
        if (!isAdmin && !roles.Contains("StoreManager"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bu kullanıcının onay yetkisi yok (Mağaza Müdürü gerekli)." });

        if (!isAdmin && (manager.StoreId is null || manager.StoreId != requester.StoreId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Sadece bu mağazanın müdürü onay verebilir." });

        if (manager.IsFirstLogin)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Yönetici varsayılan PIN'ini değiştirmeden onay veremez." });

        var token = _jwtService.GenerateApprovalToken(
            manager.Id, manager.FullName, request.Action, requester.Id);

        return Ok(new { approvalToken = token, expiresInSeconds = JwtService.ApprovalTokenLifetimeSeconds });
    }
}

public record ManagerApprovalRequest(string PersonelId, string Pin, string Action);
