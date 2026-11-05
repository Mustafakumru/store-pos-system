using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LCWaikiki.POS.WpfClient.Core.Http;

/// <summary>
/// Tüm HTTP istekleri bu merkezi client üzerinden geçer.
/// JWT token otomatik olarak Authorization header'a eklenir.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private string? _token;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Backend tarihleri UTC ("Z") gönderir; ekranda/fişte yerel saat görünsün.
        // (Eskiden fişteki saat UTC basılıyordu - 3 saat geri.)
        Converters = { new LocalDateTimeJsonConverter() }
    };
    public string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

    /// <summary>
    /// Oturum (JWT) suresi doldugunda cagrilir: kullaniciya PIN ile yeniden giris
    /// ekrani gosterir, basariliysa true doner ve bekleyen istek yeni token'la
    /// TEKRAR gonderilir (sepet/odeme ekrani kaybolmaz). Null ise yeniden giris yok.
    /// </summary>
    public Func<Task<bool>>? ReauthenticateAsync { get; set; }
    private readonly SemaphoreSlim _reauthGate = new(1, 1);

    /// <summary>Testler icin: sahte HTTP isleyicisi ve serbest zaman asimi.</summary>
    internal ApiClient(HttpMessageHandler handler, string baseUrl, TimeSpan timeout)
    {
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = timeout };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public ApiClient(string baseUrl, bool allowUntrustedLocalhostCertificate = false, int timeoutSeconds = 30)
    {
        var baseUri = new Uri(baseUrl);
        var handler = new HttpClientHandler();

        // Sertifika dogrulamasi SADECE gelistirme icin, SADECE localhost'ta
        // ve ayarla acikca izin verildiyse atlanir. Uzak sunucuda her zaman
        // normal dogrulama yapilir.
        if (allowUntrustedLocalhostCertificate && baseUri.IsLoopback)
        {
            handler.ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None
                || (request.RequestUri?.IsLoopback ?? false);
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = baseUri,
            // Varsayilan 100 sn yerine: ag takildiginda kasa uzun sure donmus gorunmesin
            Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120))
        };
        _http.DefaultRequestHeaders.Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── Token yönetimi ───────────────────────────────────────────
    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public bool HasToken => !string.IsNullOrEmpty(_token);

    /// <summary>
    /// Backend'in dondugu goreli yolu (orn. urun resmi "/images/x.jpg")
    /// API adresine gore mutlak URL'e cevirir. Resimler ayni WebAPI'nin
    /// statik dosyalarindan servis edilir.
    /// </summary>
    public string? ToAbsoluteUrl(string? relativePath)
        => string.IsNullOrWhiteSpace(relativePath)
            ? null
            : new Uri(_http.BaseAddress!, relativePath).ToString();

    // ── HTTP metodları ───────────────────────────────────────────
    public async Task<T> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, endpoint), ct);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    public async Task<T> PostAsync<T>(string endpoint, object? body = null, CancellationToken ct = default)
        => await PostAsync<T>(endpoint, body, idempotencyKey: null, ct);

    /// <summary>
    /// Para hareketi yapan POST: ayni idempotencyKey ile tekrar gonderilirse
    /// (ag koptu, yeniden denendi) sunucu islemi TEKRARLAMAZ, ilk yaniti doner.
    /// </summary>
    public async Task<T> PostAsync<T>(string endpoint, object? body, string? idempotencyKey, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => BuildPost(endpoint, body, idempotencyKey), ct);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    public async Task PostAsync(string endpoint, object? body = null, CancellationToken ct = default)
    {
        using var _ = await SendAsync(() => BuildPost(endpoint, body, null), ct);
    }

    /// <summary>Baglanti yoklamasi (kimlik gerektirmez).</summary>
    public async Task PingAsync(CancellationToken ct = default)
    {
        using var _ = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/health"), ct, allowReauth: false, log: false);
    }

    private static HttpRequestMessage BuildPost(string endpoint, object? body, string? idempotencyKey)
    {
        var json = body is null ? "{}" : JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return request;
    }

    /// <summary>
    /// Tum isteklerin tek cikis noktasi:
    ///  • Ag hatasi / zaman asimi -> anlasilir ApiException (StatusCode 0) + baglanti gostergesi
    ///  • Oturum suresi doldu (401 + invalid_token) -> PIN ile yeniden giris, istek BIR KEZ tekrar
    ///  • Her cagri log'a yazilir (yol, durum, sure - govde yazilmaz)
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct,
        bool allowReauth = true, bool log = true)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = requestFactory();
            var tokenUsed = _token;
            var sw = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                ConnectivityMonitor.Instance.Report(false);
                if (log) AppLog.Warn($"{request.Method} {request.RequestUri} zaman asimi ({sw.ElapsedMilliseconds} ms)", ex);
                throw new ApiException(0,
                    $"Sunucu {_http.Timeout.TotalSeconds:0} saniye icinde yanit vermedi. Ag baglantisini kontrol edip tekrar deneyin.");
            }
            catch (HttpRequestException ex)
            {
                ConnectivityMonitor.Instance.Report(false);
                if (log) AppLog.Warn($"{request.Method} {request.RequestUri} baglanti hatasi", ex);
                throw new ApiException(0, "Sunucuya ulasilamadi. Ag baglantisini kontrol edip tekrar deneyin.");
            }

            ConnectivityMonitor.Instance.Report(true);
            if (log)
            {
                var line = $"{request.Method} {request.RequestUri} -> {(int)response.StatusCode} ({sw.ElapsedMilliseconds} ms)";
                if ((int)response.StatusCode >= 500) AppLog.Error(line);
                else if ((int)response.StatusCode >= 400) AppLog.Warn(line);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized
                && allowReauth && attempt == 1
                && tokenUsed is not null
                && ReauthenticateAsync is not null
                && IsExpiredTokenResponse(response))
            {
                response.Dispose();
                if (await TryReauthenticateAsync(tokenUsed))
                    continue;   // ayni istek yeni token'la (idempotency anahtari ayni)
                throw new ApiException(401, "Oturumun suresi doldu. Devam etmek icin tekrar giris yapin.");
            }

            try
            {
                await EnsureSuccessAsync(response);
            }
            catch
            {
                response.Dispose();
                throw;
            }
            return response;
        }
    }

    /// <summary>
    /// Sadece SURESI DOLMUS/gecersiz token 401'i yeniden giris tetikler; yanlis
    /// PIN gibi is kurali 401'leri (orn. mudur onayi) tetiklemez.
    /// </summary>
    private static bool IsExpiredTokenResponse(HttpResponseMessage response)
        => response.Headers.WwwAuthenticate.Any(h =>
            h.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            && (h.Parameter?.Contains("invalid_token", StringComparison.OrdinalIgnoreCase) ?? false));

    private async Task<bool> TryReauthenticateAsync(string expiredToken)
    {
        await _reauthGate.WaitAsync();
        try
        {
            // Bu sirada baska bir istek zaten yeniden giris yaptiysa tekrar sorma
            if (_token is not null && _token != expiredToken) return true;
            AppLog.Info("Oturum suresi doldu, yeniden giris isteniyor.");
            var ok = await ReauthenticateAsync!();
            AppLog.Info(ok ? "Yeniden giris basarili." : "Yeniden giris iptal edildi.");
            return ok;
        }
        finally
        {
            _reauthGate.Release();
        }
    }

    // ── Hata yönetimi ────────────────────────────────────────────
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        var message = ExtractErrorMessage(body);

        if (string.IsNullOrWhiteSpace(message))
        {
            // JSON'dan anlamli bir mesaj cikaramadik: ham govdeyi goster,
            // govde de bossa ancak o zaman ReasonPhrase'e dus.
            message = !string.IsNullOrWhiteSpace(body)
                ? body
                : (response.ReasonPhrase ?? "Bilinmeyen hata");
        }

        throw new ApiException((int)response.StatusCode, message);
    }

    /// <summary>
    /// Backend'in dondugu hata govdesinden okunabilir bir mesaj cikarir.
    /// Sirasiyla su seklleri dener:
    ///   1) { "message": "..." }                              (bizim ApiErrorResponse)
    ///   2) { "errors": { "Alan": ["mesaj1","mesaj2"] } }      (ASP.NET Core ValidationProblemDetails)
    ///   3) { "detail": "..." } veya { "title": "..." }        (ASP.NET Core ProblemDetails)
    /// Hicbiri eslesmezse null doner (cagiran taraf ham govdeyi gosterir).
    /// </summary>
    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            // 1) { "message": "..." } — backend'in kendi ozel formati
            string? topMessage = null;
            if (root.TryGetProperty("message", out var msgProp)
                && msgProp.ValueKind == JsonValueKind.String)
            {
                topMessage = msgProp.GetString();
            }

            // 1b) { "detail": "..." } — ayni response'ta message ile BIRLIKTE
            // gelebilir (ornegin ExceptionHandlingMiddleware); message genel
            // baslik, detail asil sebep oldugu icin ikisi varsa birlestiririz.
            string? topDetail = null;
            if (root.TryGetProperty("detail", out var detailProp)
                && detailProp.ValueKind == JsonValueKind.String)
            {
                topDetail = detailProp.GetString();
            }

            // Ikisi ayni (ya da biri digerini iceriyor) ise tek kez goster - eskiden
            // "X bulunamadi. — X bulunamadi." seklinde iki kez yaziliyordu.
            if (!string.IsNullOrWhiteSpace(topMessage) && !string.IsNullOrWhiteSpace(topDetail))
            {
                var m = topMessage.Trim();
                var d = topDetail.Trim();
                if (string.Equals(m, d, StringComparison.OrdinalIgnoreCase) || m.Contains(d)) return m;
                if (d.Contains(m)) return d;
                return $"{m} — {d}";
            }
            if (!string.IsNullOrWhiteSpace(topDetail))
                return topDetail;
            if (!string.IsNullOrWhiteSpace(topMessage))
                return topMessage;

            // 2) ASP.NET Core ValidationProblemDetails: { "errors": { "Alan": ["mesaj", ...] } }
            if (root.TryGetProperty("errors", out var errorsProp)
                && errorsProp.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var field in errorsProp.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in field.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                lines.Add($"{field.Name}: {item.GetString()}");
                        }
                    }
                    else if (field.Value.ValueKind == JsonValueKind.String)
                    {
                        lines.Add($"{field.Name}: {field.Value.GetString()}");
                    }
                }
                if (lines.Count > 0) return string.Join(" | ", lines);
            }

            // 3) ASP.NET Core ProblemDetails fallback: { "title": "..." }
            // (detail zaten yukarida kontrol edildi)
            if (root.TryGetProperty("title", out var titleProp)
                && titleProp.ValueKind == JsonValueKind.String)
            {
                var title = titleProp.GetString();
                if (!string.IsNullOrWhiteSpace(title)) return title;
            }

            return null;
        }
        catch (JsonException)
        {
            // Govde JSON degil (duz metin/HTML olabilir) - cagiran taraf ham
            // govdeyi mesaj olarak kullanacak.
            return null;
        }
    }
}

public record ApiErrorResponse(string? Message);

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string message) : base(message)
        => StatusCode = statusCode;
}
