using Application.Features.StockTransfers.Commands.CreateStockTransfer;
using Application.Features.StockTransfers.Commands.ShipStockTransfer;
using Application.Features.StockTransfers.Commands.ReceiveStockTransfer;
using Application.Features.StockTransfers.Queries.GetStockTransfers;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.StockTransfers.Commands.CancelStockTransfer;
using Application.Common.Interfaces;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,StoreManager")]
public class StockTransfersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public StockTransfersController(ISender sender, ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Mağaza müdürünün JWT'deki mağazası. Admin için null. Mağazası olmayan
    /// müdür hesabı hiçbir sevkiyat işlemi yapamaz (Forbid).
    /// </summary>
    private int? ManagerStoreId =>
        !IsAdmin && int.TryParse(_currentUser.StoreId, out var id) ? id : null;

    /// <summary>
    /// Tüm sevkiyatları listeler.
    /// Mağaza ve duruma göre filtrele: ?storeId=1&status=Draft
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? storeId,
        [FromQuery] TransferStatus? status,
        CancellationToken ct)
    {
        // Mağaza müdürü SADECE kendi mağazasının sevkiyatlarını görür
        if (!IsAdmin)
        {
            if (ManagerStoreId is null) return Forbid();
            storeId = ManagerStoreId;
        }

        var result = await _sender.Send(
            new GetStockTransfersQuery(storeId, status), ct);
        return Ok(result);
    }

    /// <summary>
    /// Yeni sevkiyat oluşturur (Draft).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateStockTransferCommand command,
        CancellationToken ct)
    {
        // Mağaza müdürü sadece KENDİ mağazası için talep oluşturabilir
        if (!IsAdmin && (ManagerStoreId is null || command.ToStoreId != ManagerStoreId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Sadece kendi mağazanız için sevkiyat talebi oluşturabilirsiniz." });

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>
    /// Sevkiyatı gönderir (Draft → Shipped).
    /// Depo rezervasyonu bu adımda yapılır.
    /// </summary>
    [HttpPatch("{id}/ship")]
    [Authorize(Roles = "Admin")]   // Depodan çıkış merkezin işi - mağaza kendi talebini onaylayamaz
    public async Task<IActionResult> Ship(
        int id,
        ShipStockTransferCommand command,
        CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("URL id ile body id eşleşmiyor.");

        await _sender.Send(command, ct);
        return Ok(new { Message = "Sevkiyat gönderildi." });
    }
    /// <summary>
    /// Sevkiyatı iptal eder.
    /// Draft → iptal (rezervasyon yok)
    /// Shipped → iptal (rezervasyon geri alınır)
    /// Received → iptal edilemez
    /// </summary>
    [HttpPatch("{id}/cancel")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin && ManagerStoreId is null) return Forbid();

        await _sender.Send(
            new CancelStockTransferCommand(id, request.Reason, ManagerStoreId), ct);

        return Ok(new { Message = "Sevkiyat iptal edildi." });
    }
    /// <summary>
    /// Mağaza teslim aldı (Shipped → Received).
    /// Depo stoğu düşer, mağaza stoğu artar.
    /// </summary>
    [HttpPatch("{id}/receive")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Receive(
        int id,
        [FromBody] ReceiveRequest? request,
        CancellationToken ct)
    {
        if (!IsAdmin && ManagerStoreId is null) return Forbid();

        await _sender.Send(new ReceiveStockTransferCommand(
            id, ManagerStoreId, request?.Items, request?.Note), ct);
        return Ok(new { Message = "Sevkiyat teslim alındı, mağaza stoğu güncellendi." });
    }
}
public record CancelRequest(string? Reason);

/// <summary>Boş gövde ({}) = tam teslim. Items: kalem bazında fiilen alınan miktar.</summary>
public record ReceiveRequest(List<ReceiveItem>? Items, string? Note);