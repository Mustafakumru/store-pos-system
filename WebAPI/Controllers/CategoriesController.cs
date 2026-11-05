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
