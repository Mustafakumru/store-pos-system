using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace WebAPI.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hata oluştu: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        // Doğrulama hataları: alan bazlı liste (Angular apiErrorMessage ve WPF
        // ApiClient "errors" sözlüğünü okur) + tek satırlık özet mesaj.
        Dictionary<string, string[]>? errors = null;
        if (exception is FluentValidation.ValidationException validation)
        {
            errors = validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        var (statusCode, message) = exception switch
        {
            // 400 — Doğrulama (FluentValidation pipeline)
            FluentValidation.ValidationException ex =>
                (HttpStatusCode.BadRequest,
                 string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct())),
            // 400 — Metin, kolon uzunluğunu aşıyor (SQL 2628 / 8152). Validator'ı
            // olmayan bir alan için son savunma; eskiden 500 dönüyordu.
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2628 or 8152 } } =>
                (HttpStatusCode.BadRequest,
                 "Girilen metinlerden biri izin verilen uzunluğu aşıyor."),
            // 404 — Bulunamadı
            KeyNotFoundException ex =>
                (HttpStatusCode.NotFound, ex.Message),
            // 400 — Geçersiz işlem
            InvalidOperationException ex =>
                (HttpStatusCode.BadRequest, ex.Message),
            // 401 — Yetkisiz
            UnauthorizedAccessException ex =>
                (HttpStatusCode.Unauthorized, ex.Message),
            // 403 — Kimlik geçerli, bu kaynağa yetki yok
            Application.Common.Exceptions.ForbiddenAccessException ex =>
                (HttpStatusCode.Forbidden, ex.Message),
            // 409 — Eszamanli islem: yeniden denemeler de cakisti
            Application.Common.Behaviors.ConcurrencyConflictException ex =>
                (HttpStatusCode.Conflict, ex.Message),
            DbUpdateConcurrencyException =>
                (HttpStatusCode.Conflict,
                 "Kayıt başka bir işlem tarafından değiştirildi. Lütfen tekrar deneyin."),
            // 409 — Benzersizlik ihlali. SQL Server: 2601 (unique index),
            // 2627 (unique constraint). Eskiden mesajda "UNIQUE" araniyordu;
            // SQL Server "unique index" yazdigi icin hic eslesmiyor, 500 donuyordu.
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } } =>
                (HttpStatusCode.Conflict,
                 "Bu kayıt zaten mevcut. Benzersiz alan çakışması."),
            // 400 — Yabancı anahtar ihlali (547): var olmayan bir kayda bağlantı
            // (örn. silinmiş müşteri / ürün Id'si). Eskiden 500 dönüyordu.
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 547 } } =>
                (HttpStatusCode.BadRequest,
                 "İşlemde geçersiz (bulunamayan) bir kayda başvuru var. Seçimleri kontrol edip tekrar deneyin."),
            // 500 — Beklenmeyen hata
            _ => (HttpStatusCode.InternalServerError,
                  "Beklenmeyen bir hata oluştu.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var isDevelopment = context.RequestServices
            .GetRequiredService<IHostEnvironment>()
            .IsDevelopment();

        var response = new
        {
            StatusCode = (int)statusCode,
            Message = message,
            Errors = errors,
            // Production'da detay gosterilmez. Development'ta her zaman asil
            // exception.Message gosterilir (InnerException yoksa bile), varsa
            // InnerException.Message de eklenir.
            // Doğrulama hatasında Detail yok: mesaj zaten Türkçe ve tam (FluentValidation'ın
            // ham İngilizce metni istemcide mesajın yanına eklenmesin)
            Detail = isDevelopment && exception is not FluentValidation.ValidationException
                ? (exception.InnerException != null
                    ? $"{exception.Message} -> {exception.InnerException.Message}"
                    : exception.Message)
                : null
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
    }
}
