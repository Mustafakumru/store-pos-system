using Application.Features.GiftCards.Commands.LoadGiftCard;
using Application.Features.GiftCards.Queries.GetGiftCardByNumber;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pos/giftcards")]
[Authorize(Roles = "Admin,StoreManager,Cashier")]
public class GiftCardsController : ControllerBase
{
    private readonly ISender _sender;

    public GiftCardsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// WPOS "Hediye Kart Sorgulama" — kart numarasıyla bakiye sorgular.
    /// </summary>
    [HttpGet("{cardNumber}")]
    public async Task<IActionResult> GetByNumber(
        string cardNumber, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(
                new GetGiftCardByNumberQuery(cardNumber), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// WPOS "Hediye Kart Yükleme" — karta bakiye yükler.
    /// Kart sistemde yoksa otomatik oluşturulur.
    /// </summary>
    /// <summary>
    /// Hediye kartı SATIŞI (ücretli yükleme): müşteri öder, kart(lar)a bakiye yüklenir.
    /// Emanet olarak izlenir - ciroya girmez. Açık kasa oturumu gerekir.
    /// </summary>
    [HttpPost("sell")]
    [WebAPI.Common.Idempotent]
    public async Task<IActionResult> Sell(
        [FromBody] Application.Features.GiftCards.Commands.SellGiftCard.SellGiftCardCommand command,
        CancellationToken ct)
        => Ok(await _sender.Send(command, ct));

    [HttpPost("{cardNumber}/load")]
    [WebAPI.Common.Idempotent]
    // ÖDEMESİZ bakiye yükler: eskiden her kasiyer istediği karta istediği tutarı
    // yükleyebiliyordu (karşılıksız para). Müşteriye hediye kartıyla iade artık
    // iade/değişim işleminin içinde (iade defteri) yapılıyor. Ücretli hediye kartı
    // SATIŞI ayrı bir özellik olarak eklenmeli; o zamana kadar sadece Admin.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Load(
        string cardNumber,
        [FromBody] LoadGiftCardRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(
                new LoadGiftCardCommand(cardNumber, request.Amount, request.SaleId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record LoadGiftCardRequest(decimal Amount, int? SaleId = null);