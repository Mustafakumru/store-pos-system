using Application.Features.Warehouse.Commands.ReceiveStock;
using Application.Features.Warehouse.Queries.GetWarehouseStock;
using Application.Features.Warehouse.Queries.GetProductsForReceiving;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WarehouseController : ControllerBase
{
    private readonly ISender _sender;

    public WarehouseController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// Katalogdaki tüm aktif ürünleri mevcut depo stok bilgisiyle döner.
    /// Mal girişi formunda ürün seçimi için kullanılır.
    /// Katalog dışı ürün eklenemez.
    /// </summary>
    [HttpGet("products")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetProductsForReceiving(CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetProductsForReceivingQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Depodaki tüm stokları listeler.
    /// </summary>
    [HttpGet("stock")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetStock(CancellationToken ct)
    {
        var result = await _sender.Send(new GetWarehouseStockQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Depoya mal girişi yapar.
    /// Ürün katalogda yoksa hata fırlar.
    /// Ürün depoda yoksa yeni satır açar, varsa üstüne ekler.
    /// </summary>
    [HttpPost("receive")]
    [Authorize(Roles = "Admin")]   // Depoya mal girişi merkezin işi - eskiden her oturum (kasiyer dahil) yapabiliyordu
    public async Task<IActionResult> Receive(
        ReceiveStockCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }
}