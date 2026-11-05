using Application.Features.POS.Commands.CloseCashSession;
using Application.Features.POS.Commands.OpenCashSession;
using Application.Features.POS.Queries.GetCashSessionReport;
using Application.Features.POS.Queries.GetCurrentCashSession;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/cash-session")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class CashSessionsController : ControllerBase
{
    private readonly ISender _sender;

    public CashSessionsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// WPOS "Avans" — gün başı kasa açılışı (yönetici onaylı).
    /// </summary>
    [HttpPost("open")]
    public async Task<IActionResult> Open(
        [FromBody] OpenCashSessionCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(command, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// WPOS "Teslim" — gün sonu kasa kapanışı (yönetici onaylı).
    /// </summary>
    [HttpPost("close")]
    public async Task<IActionResult> Close(
        [FromBody] CloseCashSessionCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(command, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Kasiyerin açık kasa oturumu var mı? (POS satışa geçmeden kontrol eder.)
    /// Beklenen tutar dönülmez - teslim kör sayımdır.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
        => Ok(await _sender.Send(new GetCurrentCashSessionQuery(), ct));

    /// <summary>
    /// WPOS "Kasa Raporu" — açık oturumun ara raporu (beklenen kasa tutarı).
    /// SADECE müdür/Admin: teslim kör sayım olduğu için kasiyer beklenen
    /// tutarı göremez (görürse sayımı ona göre "tutturabilir").
    /// </summary>
    [HttpGet("report")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Report([FromQuery] string? cashierId, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new GetCashSessionReportQuery(cashierId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}