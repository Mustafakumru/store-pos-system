using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

namespace WebAPI.Common;

/// <summary>
/// Tekrar gonderim korumasi (para hareketi yapan uclar icin).
///
/// Istemci "Idempotency-Key" basligi gonderirse:
///  • Anahtar ilk kez geliyorsa islem yapilir, basarili yanit saklanir.
///  • Ayni kullanici ayni anahtari tekrar gonderirse (ag koptu, istemci yeniden
///    denedi, cift tiklama) islem TEKRARLANMAZ; saklanan ilk yanit aynen doner
///    ("Idempotent-Replayed: true" basligiyla).
///  • Ilk istek hala islenirken ikincisi gelirse 409 doner.
///  • Islem basarisiz olursa (4xx/5xx) kayit silinir; ayni anahtarla duzeltilip
///    tekrar denenebilir.
/// Baslik yoksa davranis degismez (eski istemcilerle uyumlu).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan StalePendingAfter = TimeSpan.FromMinutes(2);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString().Trim();
        if (string.IsNullOrEmpty(key))
        {
            await next();
            return;
        }
        if (key.Length > 100)
        {
            context.Result = new BadRequestObjectResult(new { message = "Idempotency-Key en fazla 100 karakter olabilir." });
            return;
        }

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.Identity?.Name ?? "anonymous";
        var path = $"{http.Request.Method} {http.Request.Path}";
        var scopeFactory = http.RequestServices.GetRequiredService<IServiceScopeFactory>();

        // ── 1) Anahtari ayir (ayri DbContext: islemin kendi context'ine karismasin) ──
        long recordId;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await db.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, http.RequestAborted);

            if (existing is not null)
            {
                if (existing.RequestPath != path)
                {
                    context.Result = new UnprocessableEntityObjectResult(new { message = "Bu Idempotency-Key baska bir islem icin kullanilmis." });
                    return;
                }
                if (existing.IsCompleted)
                {
                    http.Response.Headers["Idempotent-Replayed"] = "true";
                    context.Result = new ContentResult
                    {
                        StatusCode = existing.ResponseStatusCode ?? 200,
                        Content = existing.ResponseBody ?? string.Empty,
                        ContentType = existing.ResponseContentType ?? "application/json"
                    };
                    return;
                }
                if (DateTime.UtcNow - existing.CreatedAt < StalePendingAfter)
                {
                    context.Result = new ConflictObjectResult(new { message = "Bu islem hala isleniyor. Birkac saniye sonra tekrar deneyin." });
                    return;
                }
                // Yarim kalmis (sunucu cokmus) kayit: devral
                db.IdempotencyRecords.Remove(existing);
                await db.SaveChangesAsync(http.RequestAborted);
            }

            var record = new IdempotencyRecord { Key = key, UserId = userId, RequestPath = path, CreatedAt = DateTime.UtcNow };
            db.IdempotencyRecords.Add(record);
            try
            {
                await db.SaveChangesAsync(http.RequestAborted);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Ayni anahtarla es zamanli ikinci istek
                context.Result = new ConflictObjectResult(new { message = "Bu islem hala isleniyor. Birkac saniye sonra tekrar deneyin." });
                return;
            }
            recordId = record.Id;
        }

        // ── 2) Islemi calistir ──
        var executed = await next();

        // ── 3) Sonucu sakla ya da (basarisizsa) anahtari serbest birak ──
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.IdempotencyRecords.FirstAsync(r => r.Id == recordId);

            var status = executed.Result switch
            {
                ObjectResult o => o.StatusCode ?? 200,
                StatusCodeResult s => s.StatusCode,
                _ => executed.Exception is null ? 200 : 500
            };

            if (executed.Exception is not null || status >= 400)
            {
                db.IdempotencyRecords.Remove(record);
            }
            else
            {
                var json = http.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
                record.IsCompleted = true;
                record.CompletedAt = DateTime.UtcNow;
                record.ResponseStatusCode = status;
                record.ResponseContentType = "application/json; charset=utf-8";
                record.ResponseBody = executed.Result is ObjectResult obj
                    ? JsonSerializer.Serialize(obj.Value, obj.Value?.GetType() ?? typeof(object), json)
                    : string.Empty;
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
