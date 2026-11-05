using Application.Features.POS.Commands.CreateZReport;
using Application.Features.POS.Queries.GetFinancialReport;
using Application.Features.POS.Queries.GetLastSale;
using Application.Features.POS.Queries.GetXReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.POS.Commands.ReprintSale;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/reports")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// WPOS "X Raporu" — ara rapor, sayaç sıfırlanmaz.
    /// </summary>
    [HttpGet("x-report")]
    public async Task<IActionResult> GetXReport(CancellationToken ct)
    {
        // Nakit/ciro sadece müdür ve Admin'e (kasiyer için kör sayım korunur)
        var isManager = User.IsInRole("StoreManager") || User.IsInRole("Admin");
        var result = await _sender.Send(new GetXReportQuery(IncludeCashDetails: isManager), ct);
        return Ok(result);
    }

    /// <summary>
    /// WPOS "Z Raporu" — gün sonu kapanışı, sayaç sıfırlanır (yeni Z no atanır).
    /// </summary>
    [HttpPost("z-report")]
    public async Task<IActionResult> CreateZReport(
        [FromBody] ZReportRequest? request, CancellationToken ct)
    {
        // Müdür/Admin kendisi alabilir; kasiyer müdür onay token'ı ("z-report") göndermeli.
        var isManager = User.IsInRole("StoreManager") || User.IsInRole("Admin");
        var result = await _sender.Send(new CreateZReportCommand(request?.ApprovalToken, isManager), ct);
        return Ok(result);
    }

    public record ZReportRequest(string? ApprovalToken);
    /// <summary>
    /// WPOS "Mali Rapor" — Z no aralığı veya tarih aralığına göre özet.
    /// </summary>
    [HttpGet("financial")]
    public async Task<IActionResult> GetFinancialReport(
        [FromQuery] int? fromZNumber,
        [FromQuery] int? toZNumber,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(
                new GetFinancialReportQuery(fromZNumber, toZNumber, fromDate, toDate), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    /// <summary>
    /// WPOS "Son Belge Tekrarı" (1/2) — kasiyerin son fişini getirir.
    /// </summary>
    [HttpGet("last-sale")]
    public async Task<IActionResult> GetLastSale(CancellationToken ct)
    {
        var result = await _sender.Send(new GetLastSaleQuery(), ct);

        if (result is null)
            return NotFound(new { message = "Henüz tamamlanmış satış bulunamadı." });

        return Ok(result);
    }

    /// <summary>
    /// WPOS "Son Belge Tekrarı" (2/2) — yönetici onayı ile fişi tekrar yazdırır.
    /// </summary>
    [HttpPost("reprint/{saleId:int}")]
    public async Task<IActionResult> Reprint(
        int saleId,
        [FromBody] ReprintRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(
                new ReprintSaleCommand(saleId, request.ApprovalToken), ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    public record ReprintRequest(string ApprovalToken);
}