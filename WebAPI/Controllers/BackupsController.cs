using System.Security.Claims;
using Infrastructure.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>Veritabanı yedekleri: durum/geçmiş ve elle "Şimdi yedekle" (yalnızca Admin).</summary>
[ApiController]
[Route("api/backups")]
[Authorize(Roles = "Admin")]
public class BackupsController : ControllerBase
{
    private readonly DatabaseBackupService _backup;
    public BackupsController(DatabaseBackupService backup) => _backup = backup;

    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await _backup.GetStatusAsync(ct));

    /// <summary>Yedeği hemen alır ve doğrular (sonuç dönene kadar bekler).</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var who = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
        var record = await _backup.RunAsync("Elle", who, ct);
        return record.Succeeded
            ? Ok(record)
            : UnprocessableEntity(new { message = "Yedek alınamadı: " + record.Error, record });
    }
}
