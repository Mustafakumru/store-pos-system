using Application.Common.Interfaces;
using Application.Features.Stores.Commands.CreateStore;
using Application.Features.Stores.Commands.UpdateStore;
using Application.Features.Stores.Commands.ToggleStoreStatus;
using Application.Features.Stores.Queries.GetStores;
using Application.Features.Stores.Queries.GetStoreInventory;
using Application.Features.Stores.Queries.GetStockAlerts;
using Application.Features.Stores.Commands.UpdateMinStock;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // ✅ Sadece auth zorunlu, rol metot bazında
public class StoresController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;  // ✅ Eklendi

    public StoresController(
        ISender sender,
        IApplicationDbContext context)  // ✅ Inject edildi
    {
        _sender = sender;
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetStoresQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        // Mağaza müdürü SADECE kendi mağazasını görür (eskiden her mağazayı okuyabiliyordu)
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("storeId")?.Value;
            if (!int.TryParse(claim, out var myStoreId) || myStoreId != id)
                return Forbid();
        }

        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (store is null)
            return NotFound(new { message = "Mağaza bulunamadı." });

        return Ok(store);
    }

    [HttpGet("{id:int}/inventory")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetInventory(
        int id,
        [FromQuery] bool? onlyAlerts,
        CancellationToken ct)
    {
        if (User.IsInRole("StoreManager"))
        {
            var claim = User.FindFirst("storeId")?.Value;
            if (string.IsNullOrEmpty(claim) || int.Parse(claim) != id)
                return Forbid();
        }

        var result = await _sender.Send(
            new GetStoreInventoryQuery(id, onlyAlerts), ct);
        return Ok(result);
    }

    /// <summary>
    /// Mağaza varyantlarının düşük stok eşiğini günceller.
    /// Mağaza müdürü sadece kendi mağazası için.
    /// </summary>
    [HttpPut("{id:int}/inventory/min-stock")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> UpdateMinStock(
        int id,
        [FromBody] List<MinStockItem> items,
        CancellationToken ct)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("storeId")?.Value;
            if (!int.TryParse(claim, out var myStoreId) || myStoreId != id)
                return Forbid();
        }

        var updated = await _sender.Send(new UpdateMinStockCommand(id, items), ct);
        return Ok(new { updated, message = $"{updated} varyantın minimum stok seviyesi güncellendi." });
    }

    [HttpGet("stock-alerts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStockAlerts(CancellationToken ct)
    {
        var result = await _sender.Send(new GetStockAlertsQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        CreateStoreCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        UpdateStoreCommand command,
        CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("URL id ile body id eşleşmiyor.");
        await _sender.Send(command, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/toggle-status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleStatus(
        int id,
        CancellationToken ct)
    {
        var isActive = await _sender.Send(
            new ToggleStoreStatusCommand(id), ct);
        return Ok(new
        {
            IsActive = isActive,
            Message = isActive ? "Mağaza aktif edildi." : "Mağaza pasife alındı."
        });
    }
}