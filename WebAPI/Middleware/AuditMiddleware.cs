using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.Common.Auditing;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace WebAPI.Middleware;

/// <summary>
/// İşlem kaydı (istek düzeyi): sisteme YAZAN her API isteği (POST/PUT/PATCH/DELETE)
/// sonucuyla birlikte AuditLogs'a yazılır — başarılı satış da, yanlış PIN denemesi de,
/// yetkisiz istek de.
///
/// Boru hattının EN DIŞINDA çalışır: hata ara katmanının ürettiği son durum kodunu
/// ve kimlik doğrulamanın doldurduğu kullanıcıyı görür. Kayıt AYRI bir DbContext ile
/// yazılır; kayıt yazılamazsa istek bozulmaz, sunucu loguna hata düşer.
/// </summary>
public class AuditMiddleware
{
    private const int MaxCapturedBytes = 64 * 1024;
    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "PATCH", "DELETE" };

    /// <summary>Yanıttan "ne oluştu" bilgisini çıkarmak için bakılan alanlar (sırayla).</summary>
    private static readonly (string Prop, string Label)[] ResultKeys =
    [
        ("receiptNumber", "Fiş"), ("documentNumber", "Belge"), ("returnNumber", "İade"),
        ("code", "Kod"), ("personelId", "Personel"), ("filePath", "Dosya"), ("id", "Id")
    ];

    /// <summary>Route'tan kayıt kimliği olarak alınan değerler (sırayla).</summary>
    private static readonly string[] RouteKeys =
        ["receiptNumber", "variantId", "productId", "userId", "saleId", "groupId", "imageId", "cardNumber", "id"];

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, IServiceScopeFactory scopes, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _scopes = scopes;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (!AuditedMethods.Contains(context.Request.Method)
            || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var (requestDetails, loginIdentity) = await ReadRequestAsync(context.Request);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        Exception? unhandled = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            unhandled = ex;
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
            buffer.Position = 0;
            var responseText = buffer.Length is > 0 and <= MaxCapturedBytes
                               && (context.Response.ContentType?.Contains("json") ?? false)
                ? Encoding.UTF8.GetString(buffer.ToArray())
                : null;
            // Önce kayıt, sonra yanıt: istemci "başarılı" gördüğünde kayıt yazılmış olur
            await WriteAsync(context, requestDetails, loginIdentity, responseText, unhandled, sw.ElapsedMilliseconds);
            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody);
            }
        }
    }

    /// <summary>
    /// Maskelenmiş gövde + (giriş uçları için) denenen hesabın kimliği. Personelin iş
    /// e-postası/personel no'su kimin denediğini göstermek için maskesiz tutulur;
    /// gövdedeki diğer kişisel veriler maskelenir.
    /// </summary>
    private static async Task<(string? Details, string? LoginIdentity)> ReadRequestAsync(HttpRequest request)
    {
        if (request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true)
            return ("{\"dosya\":\"(yüklenen dosya kaydedilmez)\"}", null);
        if (request.ContentLength is null or 0 or > MaxCapturedBytes
            || request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
            return (null, null);

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        string? identity = null;
        if (request.Path.Value?.EndsWith("/login", StringComparison.OrdinalIgnoreCase) == true)
            identity = JsonProp(raw, "email");
        return (AuditSanitizer.SanitizeJson(raw), identity is null ? null : AuditSanitizer.Truncate(identity, 200));
    }

    private async Task WriteAsync(HttpContext context, string? requestDetails, string? loginIdentity, string? responseText,
        Exception? unhandled, long elapsedMs)
    {
        try
        {
            var status = unhandled is null ? context.Response.StatusCode : 500;
            var succeeded = status is >= 200 and < 300;
            var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var action = descriptor is null
                ? $"{context.Request.Method} {context.Request.Path}"
                : $"{descriptor.ControllerName}.{descriptor.ActionName}";

            var user = context.User;
            var (personelId, userName) = (user.FindFirstValue("personelId"), user.FindFirstValue(ClaimTypes.Name));
            // Kimliksiz uçlarda (giriş) denenen hesap gövdeden alınır (PIN/şifre zaten maskeli)
            if (user.Identity?.IsAuthenticated != true && requestDetails is not null)
            {
                personelId ??= JsonProp(requestDetails, "personelId");
                userName ??= loginIdentity;
            }

            var (entityId, summary) = Describe(context, responseText, succeeded);
            if (context.Response.Headers.ContainsKey("Idempotent-Replayed"))
                summary = AuditSanitizer.Truncate($"{summary} (tekrar gönderim — yeni işlem yapılmadı)", 500);

            var log = new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                Category = AuditCategory.Request,
                Action = action,
                Succeeded = succeeded,
                StatusCode = status,
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                PersonelId = personelId,
                UserName = userName is null ? null : AuditSanitizer.Truncate(userName, 200),
                Roles = AuditSanitizer.Truncate(string.Join(",", user.FindAll(ClaimTypes.Role).Select(c => c.Value)), 200),
                StoreId = int.TryParse(user.FindFirstValue("storeId"), out var sid) ? sid : null,
                EntityType = descriptor?.ControllerName,
                EntityId = entityId,
                Summary = summary,
                Details = requestDetails,
                HttpMethod = context.Request.Method,
                Path = AuditSanitizer.Truncate(MaskPath(context), 300),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                DurationMs = (int)Math.Min(elapsedMs, int.MaxValue),
                CorrelationId = context.TraceIdentifier
            };
            if (string.IsNullOrEmpty(log.Roles)) log.Roles = null;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Kayıt yazılamadı: isteği bozma (satış zaten kaydedildi), ama mutlaka logla
            _logger.LogError(ex, "İşlem kaydı yazılamadı: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    private static (string? EntityId, string? Summary) Describe(HttpContext context, string? responseText, bool succeeded)
    {
        string? entityId = null;
        foreach (var key in RouteKeys)
        {
            if (context.Request.RouteValues.TryGetValue(key, out var v) && v is not null)
            {
                entityId = key == "cardNumber" ? AuditSanitizer.MaskCard(v.ToString()!) : v.ToString();
                break;
            }
        }

        if (responseText is null)
            return (entityId, succeeded ? null : $"HTTP {context.Response.StatusCode}");

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (entityId, null);

            if (!succeeded)
            {
                var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()
                    : $"HTTP {context.Response.StatusCode}";
                return (entityId, AuditSanitizer.Truncate(message ?? "", 500));
            }

            foreach (var (prop, label) in ResultKeys)
            {
                var value = Find(root, prop);
                if (value is null) continue;
                entityId ??= value;
                return (AuditSanitizer.Truncate(entityId, 100), $"{label} {value}");
            }
        }
        catch (JsonException) { }
        return (entityId, null);
    }

    private static string? Find(JsonElement obj, string prop)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (!p.Name.Equals(prop, StringComparison.OrdinalIgnoreCase)) continue;
            return p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => null
            };
        }
        return null;
    }

    private static string? JsonProp(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? Find(doc.RootElement, prop) : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Yoldaki hediye kartı numarasını maskeler.</summary>
    private static string MaskPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (context.Request.RouteValues.TryGetValue("cardNumber", out var card) && card?.ToString() is { Length: > 0 } c)
            path = path.Replace(c, AuditSanitizer.MaskCard(c));
        return path;
    }
}
