using System.Collections.Concurrent;
using System.Net;
using System.Text;
using LCWaikiki.POS.WpfClient.Core.Http;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>Gelen istegin testte incelenecek kopyasi.</summary>
public sealed record SeenRequest(HttpMethod Method, string Path, string? Token, string? IdempotencyKey, string Body);

/// <summary>
/// Sunucu yerine gecen sahte HTTP isleyicisi. Her istek kaydedilir; yanit
/// testin verdigi fonksiyonla uretilir (istisna da atabilir: ag hatasi).
/// </summary>
public sealed class FakeApi : HttpMessageHandler
{
    public ConcurrentQueue<SeenRequest> Requests { get; } = new();
    public Func<SeenRequest, Task<HttpResponseMessage>> Respond { get; set; } = _ => Task.FromResult(Json(200, "{}"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var seen = new SeenRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.Headers.Authorization?.Parameter,
            request.Headers.TryGetValues("Idempotency-Key", out var k) ? k.Single() : null,
            body);
        Requests.Enqueue(seen);
        return await Respond(seen).WaitAsync(ct);
    }

    public ApiClient Client(TimeSpan? timeout = null)
        => new(this, "http://test.local/", timeout ?? TimeSpan.FromSeconds(10));

    public static HttpResponseMessage Json(int status, string json)
        => new((HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>JWT suresi dolmus yaniti (ASP.NET Core JwtBearer'in gonderdigi baslik).</summary>
    public static HttpResponseMessage Expired()
    {
        var r = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        r.Headers.WwwAuthenticate.ParseAdd("Bearer error=\"invalid_token\", error_description=\"The token expired\"");
        return r;
    }

    public static string SaleOk(string receipt = "001202611050001")
        => $$"""{"success":true,"receiptNumber":"{{receipt}}","finalAmount":100,"saleDate":"2026-11-05T10:00:00Z","negativeStockWarnings":null}""";
}
