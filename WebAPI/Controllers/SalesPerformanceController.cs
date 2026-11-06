using Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>Tüm mağazaların satış performansı ve aylık ciro hedefleri (yalnızca Admin).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class SalesPerformanceController : ControllerBase
{
    private readonly ISender _sender;
    public SalesPerformanceController(ISender sender) => _sender = sender;

    /// <summary>from/to: yerel gün (yyyy-MM-dd), iki uç dahil.</summary>
    [HttpGet("api/sales-performance")]
    public async Task<IActionResult> Get([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => Ok(await _sender.Send(new GetSalesPerformanceQuery(from, to), ct));

    [HttpGet("api/sales-targets")]
    public async Task<IActionResult> GetTargets([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _sender.Send(new GetStoreTargetsQuery(year, month), ct));

    public record SetTargetBody(int StoreId, int Year, int Month, decimal? TargetAmount);

    [HttpPut("api/sales-targets")]
    public async Task<IActionResult> SetTarget([FromBody] SetTargetBody body, CancellationToken ct)
        => Ok(await _sender.Send(new SetStoreSalesTargetCommand(body.StoreId, body.Year, body.Month, body.TargetAmount), ct));
}
