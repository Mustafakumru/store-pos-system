using Application.Features.POS.Commands.ResumeCart;
using Application.Features.POS.Commands.SuspendCart;
using Application.Features.POS.Queries.GetSuspendedCarts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/suspended-carts")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class SuspendedCartsController : ControllerBase
{
    private readonly ISender _sender;

    public SuspendedCartsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// WPOS "Askıya Al" — sepeti askıya alır.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Suspend(
        [FromBody] SuspendCartCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// WPOS "Askıdan Çağır" — mağazadaki askıdaki sepetlerin listesi.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetSuspendedCartsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Askıdaki sepeti geri çağırır (listeden kaldırılır).
    /// </summary>
    [HttpPost("{id:int}/resume")]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ResumeCartCommand(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}