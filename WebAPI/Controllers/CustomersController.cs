using Application.Features.Customers.Commands.CreateCustomer;
using Application.Features.Customers.Queries.SearchCustomerByPhone;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/customers")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// Fiş ekranı "Müşteri Ara" — telefon numarasıyla sorgular.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string phone,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { message = "Telefon numarası gereklidir." });

        var result = await _sender.Send(
            new SearchCustomerByPhoneQuery(phone), ct);

        if (result is null)
            return NotFound(new { message = "Müşteri bulunamadı." });

        return Ok(result);
    }

    /// <summary>
    /// Fiş ekranı "Yeni Müşteri" — yeni müşteri kaydı oluşturur.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCustomerCommand command,
        CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(command, ct);
            return CreatedAtAction(
                nameof(Search), new { phone = result.PhoneNumber }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}