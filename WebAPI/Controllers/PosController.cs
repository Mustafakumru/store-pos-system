using Application.Common.Interfaces;
using Application.Features.POS.Commands.ProcessExchange;
using Application.Features.POS.Commands.ProcessRefund;
using Application.Features.POS.Commands.ProcessSale;
using Application.Features.POS.Queries.GetProductForPos;
using Application.Features.POS.Queries.GetSaleByReceipt;
using Application.Features.POS.Queries.GetSalesHistory;
using Application.Features.POS.Queries.GetSalesBreakdown;
using Application.Features.POS.Queries.GetZReports;
using Application.Features.POS.Queries.GetCashSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.POS.Queries.GetZReport;
using Application.Features.POS.Queries.GetCustomerPurchaseHistory;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers;

public record RefundBody(
    List<RefundItem> Items,
    string? Reason,
    Application.Features.POS.Common.RefundMethodDto? PayoutMethod = null,  // varsayılan: Nakit
    string? GiftCardNumber = null,
    string? RefundApprovalToken = null   // nakit payını aşan nakit iade için müdür onayı ("refund-cash")
);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Cashier,StoreManager,Admin")]
public class PosController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<AppUser> _userManager;

    public PosController(ISender sender, ICurrentUserService currentUser, UserManager<AppUser> userManager)
    {
        _sender = sender;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    /// <summary>
    /// Barkod okutunca ürün + fiyat + mağaza stoğu döner.
    /// StoreId JWT'den otomatik alınır.
    /// Hedef: < 50ms
    /// </summary>
    [HttpGet("product/{barcode}")]
    public async Task<IActionResult> GetProductByBarcode(
        string barcode,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            return Unauthorized(new { message = "Mağaza yetkisi gerekli." });

        var storeId = int.Parse(_currentUser.StoreId);
        var result = await _sender.Send(
            new GetProductForPosQuery(barcode, storeId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Barkodsuz urun bulma: urun kodu (W6ER01Z8) -> renkler -> bedenler
    /// (fiyat + magaza stogu). WPOS Fiyat Gor / Fis ekrani ve mobil uygulama kullanir.
    /// </summary>
    [HttpGet("products/by-code/{code}")]
    public async Task<IActionResult> GetProductByCode(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            return Unauthorized(new { message = "Mağaza yetkisi gerekli." });
        var result = await _sender.Send(
            new Application.Features.POS.Queries.ProductLookup.GetProductByCodeQuery(code, int.Parse(_currentUser.StoreId)), ct);
        return Ok(result);
    }

    /// <summary>Urun kodu parcasi veya ad ile arama (en fazla 20 sonuc).</summary>
    [HttpGet("products/search")]
    public async Task<IActionResult> SearchProducts([FromQuery] string q, CancellationToken ct)
        => Ok(await _sender.Send(new Application.Features.POS.Queries.ProductLookup.SearchProductsQuery(q), ct));

    /// <summary>
    /// Sepeti satış olarak kaydeder.
    /// Negatif stoka izin verilir, uyarı döner.
    /// </summary>
    [HttpPost("sales")]
    [WebAPI.Common.Idempotent]
    public async Task<IActionResult> ProcessSale(
        ProcessSaleCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Fiş numarasıyla satış sorgular.
    /// İade için kullanılır.
    /// </summary>
    [HttpGet("sales/{receiptNumber}")]
    public async Task<IActionResult> GetSaleByReceipt(
        string receiptNumber,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetSaleByReceiptQuery(receiptNumber, FullAccess: User.IsInRole("Admin")), ct);
        return Ok(result);
    }

    /// <summary>
    /// Fiyat sorgulama — sepete eklemeden sadece fiyat gösterir.
    /// </summary>
    [HttpGet("price-check/{barcode}")]
    public async Task<IActionResult> PriceCheck(
        string barcode,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            return Unauthorized(new { message = "Mağaza yetkisi gerekli." });

        var storeId = int.Parse(_currentUser.StoreId);
        var result = await _sender.Send(
            new GetProductForPosQuery(barcode, storeId), ct);

        // Sadece fiyat bilgisini döndür
        return Ok(new
        {
            result.Barcode,
            result.Name,
            result.CurrentPrice,
            result.StockQuantity
        });
    }
    // ── Satış Geçmişi ────────────────────────────────────────────
    /// <summary>
    /// Mağaza satış geçmişi — tarih bazlı + sayfalı.
    /// Kasiyer: sadece kendi mağazası (JWT'den)
    /// Admin: storeId query param ile filtreli
    /// </summary>
    [HttpGet("sales")]
    [Authorize(Roles = "StoreManager,Admin")]
    public async Task<IActionResult> GetSalesHistory(
        [FromQuery] int? storeId,
        [FromQuery] DateTime? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        // StoreManager SADECE kendi mağazasını görür. Mağazası olmayan bir
        // müdür hesabı tüm mağazaları görmesin diye reddedilir.
        int? targetStoreId;
        if (User.IsInRole("Admin"))
            targetStoreId = storeId;
        else if (!string.IsNullOrEmpty(_currentUser.StoreId))
            targetStoreId = int.Parse(_currentUser.StoreId);
        else
            return Forbid();

        var result = await _sender.Send(
            new GetSalesHistoryQuery(targetStoreId, date, page, pageSize, from, to), ct);

        // Kasiyer adları Identity'den (Application katmanı kullanıcı tablosunu görmez)
        var cashierIds = result.Items.Select(i => i.CashierId).Distinct().ToList();
        var names = await _userManager.Users
            .Where(u => cashierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        return Ok(result with
        {
            Items = result.Items
                .Select(i => i with { CashierName = names.GetValueOrDefault(i.CashierId) })
                .ToList()
        });
    }

    // ── Satış Kırılımı (saatlik / günlük / kasiyer) ─────────────
    [HttpGet("sales-breakdown")]
    [Authorize(Roles = "StoreManager,Admin")]
    public async Task<IActionResult> GetSalesBreakdown(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? storeId,
        [FromQuery] int tzOffsetMinutes = 0,
        CancellationToken ct = default)
    {
        if (!TryResolveStoreScope(storeId, requireStore: false, out var targetStoreId))
            return Forbid();

        var result = await _sender.Send(
            new GetSalesBreakdownQuery(targetStoreId, from, to, tzOffsetMinutes), ct);

        var names = await GetCashierNamesAsync(result.Cashiers.Select(c => c.CashierId), ct);
        return Ok(result with
        {
            Cashiers = result.Cashiers
                .Select(c => c with { CashierName = names.GetValueOrDefault(c.CashierId) })
                .ToList()
        });
    }

    // ── Kaydedilmiş Z Raporları ─────────────────────────────────
    [HttpGet("z-reports")]
    [Authorize(Roles = "StoreManager,Admin")]
    public async Task<IActionResult> GetZReports(
        [FromQuery] int? storeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        if (!TryResolveStoreScope(storeId, requireStore: true, out var targetStoreId))
            return User.IsInRole("Admin")
                ? BadRequest(new { message = "Admin için storeId zorunludur." })
                : Forbid();

        var result = await _sender.Send(
            new GetZReportsQuery(targetStoreId!.Value, from, to, take), ct);

        var names = await GetCashierNamesAsync(result.Select(z => z.CashierId), ct);
        return Ok(result
            .Select(z => z with { CashierName = names.GetValueOrDefault(z.CashierId) })
            .ToList());
    }

    // ── Kasa Oturumları (yönetici görünümü) ─────────────────────
    [HttpGet("cash-sessions")]
    [Authorize(Roles = "StoreManager,Admin")]
    public async Task<IActionResult> GetCashSessions(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? storeId,
        CancellationToken ct = default)
    {
        if (!TryResolveStoreScope(storeId, requireStore: true, out var targetStoreId))
            return User.IsInRole("Admin")
                ? BadRequest(new { message = "Admin için storeId zorunludur." })
                : Forbid();

        var result = await _sender.Send(
            new GetCashSessionsQuery(targetStoreId!.Value, from, to), ct);

        var names = await GetCashierNamesAsync(
            result.SelectMany(s => new[] { s.CashierId, s.OpeningApprovedByUserId ?? "", s.ClosingApprovedByUserId ?? "" }),
            ct);

        return Ok(result.Select(s => s with
        {
            CashierName = names.GetValueOrDefault(s.CashierId),
            OpeningApprovedByName = s.OpeningApprovedByUserId is null ? null : names.GetValueOrDefault(s.OpeningApprovedByUserId),
            ClosingApprovedByName = s.ClosingApprovedByUserId is null ? null : names.GetValueOrDefault(s.ClosingApprovedByUserId)
        }).ToList());
    }

    /// <summary>
    /// Admin: istekteki storeId (requireStore ise zorunlu). StoreManager: JWT'deki
    /// mağazası; mağazası yoksa false (Forbid).
    /// </summary>
    private bool TryResolveStoreScope(int? requested, bool requireStore, out int? storeId)
    {
        if (User.IsInRole("Admin"))
        {
            storeId = requested;
            return !requireStore || requested.HasValue;
        }
        storeId = int.TryParse(_currentUser.StoreId, out var id) ? id : null;
        return storeId.HasValue;
    }

    private async Task<Dictionary<string, string>> GetCashierNamesAsync(
        IEnumerable<string> ids, CancellationToken ct)
    {
        var idList = ids.Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
        if (idList.Count == 0) return new();
        return await _userManager.Users
            .Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);
    }

    [HttpGet("customers/{customerId:int}/purchases")]
    public async Task<ActionResult<List<CustomerPurchaseDto>>> GetCustomerPurchases(
      int customerId, [FromQuery] string barcode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return BadRequest("Barkod zorunludur.");

        var result = await _sender.Send(
            new GetCustomerPurchaseHistoryQuery(customerId, barcode), ct);
        return Ok(result);
    }
    // ── Değişim ──────────────────────────────────────────────────
    /// <summary>
    /// Değişimi (eski ürün iadesi + yeni ürün satışı) TEK atomik islemde
    /// yapar. Musteriye kalan farkin fiili odenmesi (nakit/kart elden,
    /// ya da fissiz iadede hediye kartina yukleme) bu endpoint'in disinda,
    /// ayri bir takip adimidir (AmountOwedToCustomer donen deger uzerinden).
    /// </summary>
    [HttpPost("exchange")]
    [WebAPI.Common.Idempotent]
    public async Task<IActionResult> ProcessExchange(
        ProcessExchangeCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    // ── İade ─────────────────────────────────────────────────────
    /// <summary>
    /// Fiş numarasıyla iade işlemi.
    /// Tam veya kısmi iade desteklenir.
    /// Stok otomatik güncellenir.
    /// Not: Cashier de yapabilir - class seviyesindeki yetki yeterli,
    /// method seviyesinde ayrica StoreManager/Admin kisitlamasi YOK
    /// (bilerek kaldirildi - magaza politikasi geregi).
    /// </summary>
    [HttpPost("sales/{receiptNumber}/refund")]
    [WebAPI.Common.Idempotent]
    public async Task<IActionResult> ProcessRefund(
        string receiptNumber,
        [FromBody] RefundBody body,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new ProcessRefundCommand(
                receiptNumber,
                body.Items,
                body.Reason,
                body.PayoutMethod,
                body.GiftCardNumber,
                body.RefundApprovalToken), ct);

        return Ok(result);
    }

    // ── Z Raporu ─────────────────────────────────────────────────
    /// <summary>
    /// Gün sonu Z raporu.
    /// Nakit/Kart dağılımı, iade özeti, saatlik satış.
    /// </summary>
    [HttpGet("z-report")]
    [Authorize(Roles = "StoreManager,Admin")]
    public async Task<IActionResult> GetZReport(
        [FromQuery] int? storeId,
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        var targetStoreId = User.IsInRole("Admin")
            ? (storeId ?? throw new InvalidOperationException(
                "Admin için storeId zorunludur."))
            : int.Parse(_currentUser.StoreId!);

        var result = await _sender.Send(
            new GetZReportQuery(targetStoreId, date), ct);

        return Ok(result);
    }
}