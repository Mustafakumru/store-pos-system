using Application.Features.Products.Commands.AddVariant;
using Application.Features.Products.Commands.CreateProduct;
using Application.Features.Products.Commands.DeleteProduct;
using Application.Features.Products.Commands.DeleteVariant;
using Application.Features.Products.Commands.GenerateVariants;
using Application.Features.Products.Queries.ProductCodes;
using Application.Features.POS.Queries.ProductLookup;
using Domain.Enums;
using Application.Features.Products.Commands.ManageVariantGroup;
using Application.Features.Products.Commands.UpdateProduct;
using Application.Features.Products.Commands.UpdateProductPrice;
using Application.Features.Products.Commands.UpdateVariant;
using Application.Features.Products.Queries.GetAllProducts;
using Application.Features.Products.Queries.GetProductByBarcode;
using Application.Features.Products.Queries.GetProductById;
using Application.Features.Products.Queries.GetVariantGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender)
        => _sender = sender;

    // ── Ürün Listesi ─────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetAllProductsQuery(), ct);
        return Ok(result);
    }

    // ── Barkod Sorgula ───────────────────────────────────────
    [HttpGet("barcode/{barcode}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetByBarcode(
        string barcode, CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetProductByBarcodeQuery(barcode), ct);
        return Ok(result);
    }

    // ── ID ile Ürün ──────────────────────────────────────────
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetById(
        int id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetProductByIdQuery(id), ct);
        return Ok(result);
    }

    // ── Ürün Oluştur ─────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // ── Urun Kodu: onizleme (sayac tuketmez) ─────────────────
    // GET api/products/code-preview?season=1&seasonYear=2026&categoryId=2&department=2
    [HttpGet("code-preview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewCode(
        [FromQuery] Season season, [FromQuery] int seasonYear,
        [FromQuery] int categoryId, [FromQuery] Department department, CancellationToken ct)
        => Ok(await _sender.Send(new PreviewProductCodeQuery(season, seasonYear, categoryId, department), ct));

    // ── Urun Kodu: coz (W6ER01Z8 -> sezon/yil/kategori/yas kitlesi) ──
    [HttpGet("code-info/{code}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> DecodeCode(string code, CancellationToken ct)
        => Ok(await _sender.Send(new DecodeProductCodeQuery(code), ct));

    // ── Kod / ad ile arama ──
    [HttpGet("search")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
        => Ok(await _sender.Send(new SearchProductsQuery(q), ct));

    // ── Renk x Beden matrisinden varyant uret (barkodlar sunucuda) ──
    [HttpPost("{productId:int}/variants/generate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateVariants(
        int productId, [FromBody] GenerateVariantsCommand command, CancellationToken ct)
    {
        if (productId != command.ProductId)
            return BadRequest(new { message = "Route id ile body id eşleşmiyor." });
        return Ok(await _sender.Send(command, ct));
    }

    // ── Ürün Güncelle ────────────────────────────────────────
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateProductCommand command,
        CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(new { message = "Route id ile body id eşleşmiyor." });

        await _sender.Send(command, ct);
        return NoContent();
    }

    // ── Ürün Sil (Soft Delete) ───────────────────────────────
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        int id, CancellationToken ct)
    {
        await _sender.Send(new DeleteProductCommand(id), ct);
        return NoContent();
    }

    // ── Varyant Ekle ─────────────────────────────────────────
    [HttpPost("{productId:int}/variants")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddVariant(
        int productId,
        [FromBody] AddVariantCommand command,
        CancellationToken ct)
    {
        if (productId != command.ProductId)
            return BadRequest(new { message = "Route id ile body id eşleşmiyor." });

        var variantId = await _sender.Send(command, ct);
        return Ok(new { variantId });
    }

    // ── Varyant Güncelle ─────────────────────────────────────
    [HttpPut("variants/{variantId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVariant(
        int variantId,
        [FromBody] UpdateVariantCommand command,
        CancellationToken ct)
    {
        if (variantId != command.Id)
            return BadRequest(new { message = "Route id ile body id eşleşmiyor." });

        await _sender.Send(command, ct);
        return NoContent();
    }

    // ── Varyant Fiyat Güncelle ───────────────────────────────
    [HttpPut("variants/{variantId:int}/price")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVariantPrice(
        int variantId,
        [FromBody] UpdateProductPriceCommand command,
        CancellationToken ct)
    {
        if (variantId != command.VariantId)
            return BadRequest(new { message = "Route id ile body id eşleşmiyor." });

        await _sender.Send(command, ct);
        return NoContent();
    }

    // ── Varyant Sil (Soft Delete) ────────────────────────────
    [HttpDelete("variants/{variantId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteVariant(
        int variantId, CancellationToken ct)
    {
        await _sender.Send(new DeleteVariantCommand(variantId), ct);
        return NoContent();
    }

    // ── Varyant Grupları ─────────────────────────────────────

    /// <summary>
    /// Ürünün renk gruplarını ve görsellerini getirir.
    /// </summary>
    [HttpGet("{productId:int}/groups")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetVariantGroups(
        int productId, CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetVariantGroupsQuery(productId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Yeni renk grubu oluşturur. Örn: "Kırmızı"
    /// </summary>
    [HttpPost("{productId:int}/groups")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateVariantGroup(
        int productId,
        [FromBody] CreateGroupBody body,
        CancellationToken ct)
    {
        var groupId = await _sender.Send(
            new CreateVariantGroupCommand(
                productId, body.GroupName, body.GroupType), ct);
        return Ok(new { groupId });
    }

    /// <summary>
    /// Varyantı bir renk grubuna atar.
    /// </summary>
    [HttpPatch("variants/{variantId:int}/group/{groupId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignVariantToGroup(
        int variantId,
        int groupId,
        CancellationToken ct)
    {
        await _sender.Send(
            new AssignVariantToGroupCommand(variantId, groupId), ct);
        return NoContent();
    }

    /// <summary>
    /// Renk grubuna görsel ekler.
    /// </summary>
    [HttpPost("groups/{groupId:int}/images")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddImageToGroup(
        int groupId,
        IFormFile file,
        [FromQuery] int displayOrder = 0,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Dosya seçilmedi." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Geçersiz dosya formatı." });

        var uploadsFolder = Path.Combine(
            Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var imagePath = $"/images/products/{fileName}";

        var imageId = await _sender.Send(
            new AddImageToGroupCommand(groupId, imagePath, displayOrder), ct);

        return Ok(new { imageId, imagePath });
    }

    /// <summary>
    /// Grup görselini siler.
    /// </summary>
    [HttpDelete("groups/images/{imageId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGroupImage(
        int imageId, CancellationToken ct)
    {
        await _sender.Send(new DeleteGroupImageCommand(imageId), ct);
        return NoContent();
    }

    // ── Fotoğraf Yükle ───────────────────────────────────────
    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Dosya seçilmedi." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new
            {
                message = "Sadece jpg, jpeg, png, webp kabul edilir."
            });

        var uploadsFolder = Path.Combine(
            Directory.GetCurrentDirectory(), "wwwroot", "images", "products");

        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok(new { imagePath = $"/images/products/{uniqueFileName}" });
    }
}

// ── Request Body'ler ─────────────────────────────────────────
public record CreateGroupBody(
    string GroupName,
    string GroupType = "Color"
);