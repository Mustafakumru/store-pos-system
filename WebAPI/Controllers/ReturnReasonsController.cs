using Application.Features.POS.Queries.GetReturnReasons;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/return-reasons")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class ReturnReasonsController : ControllerBase
{
    private readonly ISender _sender;

    public ReturnReasonsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// WPOS "İade Nedeni" listesi — kasiyer ekranında dropdown/grid.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetReturnReasonsQuery(), ct);
        return Ok(result);
    }
}