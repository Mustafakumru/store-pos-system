using Application.Common.Auditing;
using Application.Features.Audit;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>İşlem kaydı (salt okunur). Kasiyer erişemez; müdür kendi mağazasını görür.</summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin,StoreManager")]
public class AuditLogsController : ControllerBase
{
    private readonly ISender _sender;
    public AuditLogsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? storeId,
        [FromQuery] string? user, [FromQuery] string? action, [FromQuery] string? entityId,
        [FromQuery] bool? succeeded, [FromQuery] AuditCategory? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetAuditLogsQuery(from, to, storeId, user, action, entityId,
            succeeded, category, page, pageSize), ct));

    /// <summary>Filtre listesi için işlem türleri.</summary>
    [HttpGet("actions")]
    public IActionResult Actions()
        => Ok(AuditActionCatalog.All
            .Select(kv => new AuditActionOption(kv.Key, kv.Value))
            .OrderBy(a => a.Label));
}
