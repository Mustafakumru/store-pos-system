using Application.Features.Coupons.Commands.CreateCoupon;
using Application.Features.Coupons.Commands.SetCouponActive;
using Application.Features.Coupons.Queries.GetCoupons;
using Application.Features.Coupons.Queries.ValidateCoupon;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
public class CouponsController : ControllerBase
{
    private readonly ISender _sender;

    public CouponsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// POS ödeme ekranı - kupon kodunu verilen tutar için doğrular ve
    /// uygulanacak indirimi döner (ön izleme; satışta yeniden hesaplanır).
    /// </summary>
    [HttpGet("api/pos/coupons/{code}/validate")]
    [Authorize(Roles = "Admin,StoreManager,Cashier")]
    public async Task<IActionResult> Validate(string code, [FromQuery] decimal amount, CancellationToken ct)
        => Ok(await _sender.Send(new ValidateCouponQuery(code, amount), ct));

    // ── Yönetim (Admin) ─────────────────────────────────────────

    [HttpGet("api/coupons")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _sender.Send(new GetCouponsQuery(), ct));

    [HttpPost("api/coupons")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCouponCommand command, CancellationToken ct)
        => Ok(await _sender.Send(command, ct));

    [HttpPut("api/coupons/{id:int}/active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetCouponActiveRequest request, CancellationToken ct)
        => Ok(await _sender.Send(new SetCouponActiveCommand(id, request.IsActive), ct));
}

public record SetCouponActiveRequest(bool IsActive);
