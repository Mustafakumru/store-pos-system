using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LCWaikiki.POS.IntegrationTests.Infrastructure;

/// <summary>Yanıt: durum kodu + JSON gövde (boşsa default).</summary>
public sealed record ApiResponse(HttpStatusCode Status, JsonElement Body, string Raw)
{
    public int Code => (int)Status;
    public bool Ok => Code is >= 200 and < 300;

    public string Str(string prop) => Body.GetProperty(prop).GetString()!;
    public int Int(string prop) => Body.GetProperty(prop).GetInt32();
    public decimal Dec(string prop) => Body.GetProperty(prop).GetDecimal();
    public bool Bool(string prop) => Body.GetProperty(prop).GetBoolean();
    public string? Message => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("message", out var m) ? m.GetString() : null;

    /// <summary>Doğrulama hatasındaki alan adları (camelCase).</summary>
    public IReadOnlyCollection<string> ErrorFields =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object
            ? e.EnumerateObject().Select(p => p.Name).ToArray()
            : Array.Empty<string>();

    public override string ToString() => $"{Code}: {Raw}";
}

/// <summary>Test HTTP istemcisi (in-memory TestServer).</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public async Task<ApiResponse> SendAsync(HttpMethod method, string url, object? body = null,
        string? token = null, string? idempotencyKey = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null)
            req.Headers.Add("Idempotency-Key", idempotencyKey);

        using var res = await _http.SendAsync(req);
        var raw = await res.Content.ReadAsStringAsync();
        JsonElement json = default;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try { json = JsonDocument.Parse(raw).RootElement.Clone(); }
            catch (JsonException) { }
        }
        return new ApiResponse(res.StatusCode, json, raw);
    }

    public Task<ApiResponse> Get(string url, string? token = null) => SendAsync(HttpMethod.Get, url, null, token);
    public Task<ApiResponse> Post(string url, object? body, string? token = null, string? idem = null) => SendAsync(HttpMethod.Post, url, body ?? new { }, token, idem);
    public Task<ApiResponse> Put(string url, object body, string? token = null) => SendAsync(HttpMethod.Put, url, body, token);
    public Task<ApiResponse> Patch(string url, object? body, string? token = null) => SendAsync(HttpMethod.Patch, url, body, token);
}
