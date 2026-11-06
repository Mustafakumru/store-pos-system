using Domain.Common;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.Name, c.Code })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    public record CategoryBody(string Name, string? Description, string? Code);

    /// <summary>
    /// Yönetim listesi: ürün sayıları ve hangi işlemlere izin verildiği
    /// (kodlu ürün varsa kod değişmez, ürün varsa silinmez).
    /// </summary>
    [HttpGet("manage")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetForManagement(CancellationToken ct)
    {
        var rows = await _context.Categories
            .Where(c => !c.IsDeleted)
            .Select(c => new
            {
                c.Id, c.Name, c.Code, c.Description,
                ProductCount = _context.Products.Count(p => p.CategoryId == c.Id && !p.IsDeleted),
                CodedProductCount = _context.Products.IgnoreQueryFilters().Count(p => p.CategoryId == c.Id && p.Code != null)
            })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(c => new
        {
            c.Id, c.Name, c.Code, c.Description, c.ProductCount, c.CodedProductCount,
            CanChangeCode = c.CodedProductCount == 0,
            CanDelete = c.ProductCount == 0
        }));
    }

    /// <summary>Kod önerisi (form yazılırken): addan 2 harf, kullanılmayan.</summary>
    [HttpGet("suggest-code")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SuggestCode([FromQuery] string name, CancellationToken ct)
        => Ok(new { code = Application.Features.Products.Common.CategoryCodeSuggester.Suggest(name ?? "", await UsedCodesAsync(ct)) });

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CategoryBody body, CancellationToken ct)
    {
        var (name, error) = ValidateName(body.Name);
        if (error is not null) return BadRequest(new { message = error });
        if (await NameTakenAsync(name, null, ct))
            return Conflict(new { message = $"'{name}' adlı bir kategori zaten var." });

        string code;
        if (string.IsNullOrWhiteSpace(body.Code))
            code = Application.Features.Products.Common.CategoryCodeSuggester.Suggest(name, await UsedCodesAsync(ct));
        else if (!ProductCode.IsValidCategoryCode(body.Code))
            return BadRequest(new { message = "Kategori kodu 2 Latin harften oluşmalıdır (örn. PN, GM)." });
        else
            code = body.Code.Trim().ToUpperInvariant();
        if (await _context.Categories.IgnoreQueryFilters().AnyAsync(c => c.Code == code, ct))
            return Conflict(new { message = $"'{code}' kodu başka bir kategoride kullanılıyor." });

        var category = new Domain.Entities.Category { Name = name, Code = code, Description = Clean(body.Description) };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(ct);
        return StatusCode(201, new { category.Id, category.Name, category.Code, category.Description });
    }

    /// <summary>Ad ve açıklama (kod ayrı uçtan: PUT {id}/code).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryBody body, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound(new { message = "Kategori bulunamadı." });
        var (name, error) = ValidateName(body.Name);
        if (error is not null) return BadRequest(new { message = error });
        if (await NameTakenAsync(name, id, ct))
            return Conflict(new { message = $"'{name}' adlı bir kategori zaten var." });

        category.Name = name;
        category.Description = Clean(body.Description);
        await _context.SaveChangesAsync(ct);
        return Ok(new { category.Id, category.Name, category.Code, category.Description });
    }

    /// <summary>
    /// Silme: kategoride (silinmemiş) ürün varsa reddedilir. Hiç ürün görmemiş kategorinin
    /// kodu serbest bırakılır; kodlu ürün geçmişi varsa kod rezerve kalır (eski ürün kodları karışmasın).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound(new { message = "Kategori bulunamadı." });
        var productCount = await _context.Products.CountAsync(p => p.CategoryId == id && !p.IsDeleted, ct);
        if (productCount > 0)
            return Conflict(new { message = $"Bu kategoride {productCount} ürün var. Önce ürünleri başka kategoriye taşıyın ya da silin." });

        var everHadProducts = await _context.Products.IgnoreQueryFilters().AnyAsync(p => p.CategoryId == id, ct);
        category.IsDeleted = true;
        if (!everHadProducts) category.Code = null;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static (string Name, string? Error) ValidateName(string? raw)
    {
        var name = string.Join(' ', (raw ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2) return (name, "Kategori adı en az 2 karakter olmalıdır.");
        if (name.Length > 60) return (name, "Kategori adı en fazla 60 karakter olabilir.");
        return (name, null);
    }

    private static string? Clean(string? description)
    {
        var d = description?.Trim();
        return string.IsNullOrEmpty(d) ? null : d.Length > 300 ? d[..300] : d;
    }

    private async Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return await _context.Categories.AnyAsync(c => !c.IsDeleted && c.Id != exceptId && c.Name.ToLower() == lower, ct);
    }

    private async Task<List<string>> UsedCodesAsync(CancellationToken ct)
        => await _context.Categories.IgnoreQueryFilters().Where(c => c.Code != null).Select(c => c.Code!).ToListAsync(ct);

    public record UpdateCategoryCodeBody(string Code);

    /// <summary>
    /// Kategorinin urun kodundaki 2 harfli kodunu degistirir (W6[ER]01Z8).
    /// Bu kategoride kodlu urun varsa degistirilemez - mevcut kodlar anlamini yitirir.
    /// </summary>
    [HttpPut("{id:int}/code")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCode(int id, [FromBody] UpdateCategoryCodeBody body, CancellationToken ct)
    {
        if (!ProductCode.IsValidCategoryCode(body.Code))
            return BadRequest(new { message = "Kategori kodu 2 Latin harften olusmalidir (orn. PN, GM)." });
        var code = body.Code.Trim().ToUpperInvariant();

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound(new { message = "Kategori bulunamadi." });
        if (category.Code == code) return Ok(new { category.Id, category.Name, category.Code });

        if (await _context.Categories.IgnoreQueryFilters().AnyAsync(c => c.Id != id && c.Code == code, ct))
            return Conflict(new { message = $"'{code}' kodu baska bir kategoride kullaniliyor." });
        if (await _context.Products.IgnoreQueryFilters().AnyAsync(p => p.CategoryId == id && p.Code != null, ct))
            return Conflict(new { message = "Bu kategoride kodlu urun var; kategori kodu artik degistirilemez." });

        category.Code = code;
        await _context.SaveChangesAsync(ct);
        return Ok(new { category.Id, category.Name, category.Code });
    }
}
