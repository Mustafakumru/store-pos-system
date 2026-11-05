using LCWaikiki.POS.WpfClient.Core.Http;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>
/// Kasa uygulamasinin sunucuyla konustugu tek nokta: oturum yenileme,
/// tekrar gonderimde ayni islem anahtari, ag hatalari ve baglanti gostergesi.
/// ConnectivityMonitor tekil oldugu icin bu siniftaki testler sirali calisir.
/// </summary>
[Collection("ApiClient")]
public class ApiClientTests
{
    private sealed record Dto(string Value);

    [Fact]
    public async Task Expired_token_triggers_reauth_and_retries_with_same_idempotency_key()
    {
        var api = new FakeApi();
        var client = api.Client();
        client.SetToken("eski");
        var reauthCalls = 0;
        client.ReauthenticateAsync = () => { reauthCalls++; client.SetToken("yeni"); return Task.FromResult(true); };
        api.Respond = r => Task.FromResult(r.Token == "eski" ? FakeApi.Expired() : FakeApi.Json(200, """{"value":"tamam"}"""));

        var result = await client.PostAsync<Dto>("api/pos/sales", new { a = 1 }, "anahtar-1");

        Assert.Equal("tamam", result.Value);
        Assert.Equal(1, reauthCalls);
        var sent = api.Requests.ToArray();
        Assert.Equal(2, sent.Length);
        Assert.Equal(["eski", "yeni"], sent.Select(r => r.Token));
        // Ayni anahtar: ilk istek sunucuya ulasip islendiyse ikinci gonderim yeni fis kesmez
        Assert.All(sent, r => Assert.Equal("anahtar-1", r.IdempotencyKey));
        Assert.Equal(sent[0].Body, sent[1].Body);
    }

    [Fact]
    public async Task Business_401_like_wrong_manager_pin_does_not_ask_for_relogin()
    {
        var api = new FakeApi { Respond = _ => Task.FromResult(FakeApi.Json(401, """{"message":"Yonetici PIN hatali."}""")) };
        var client = api.Client();
        client.SetToken("gecerli");
        var reauthCalls = 0;
        client.ReauthenticateAsync = () => { reauthCalls++; return Task.FromResult(true); };

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.PostAsync<Dto>("api/pos/manager-approval", new { }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("Yonetici PIN hatali", ex.Message);
        Assert.Equal(0, reauthCalls);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Cancelled_reauth_gives_clear_message_and_does_not_resend()
    {
        var api = new FakeApi { Respond = _ => Task.FromResult(FakeApi.Expired()) };
        var client = api.Client();
        client.SetToken("eski");
        client.ReauthenticateAsync = () => Task.FromResult(false);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<Dto>("api/pos/product/1"));

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("Oturumun suresi doldu", ex.Message);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Concurrent_expired_requests_ask_for_pin_only_once()
    {
        var api = new FakeApi();
        var client = api.Client();
        client.SetToken("eski");
        var reauthCalls = 0;
        client.ReauthenticateAsync = async () =>
        {
            Interlocked.Increment(ref reauthCalls);
            await Task.Delay(100);          // kasiyer PIN giriyor
            client.SetToken("yeni");
            return true;
        };
        api.Respond = r => Task.FromResult(r.Token == "eski" ? FakeApi.Expired() : FakeApi.Json(200, """{"value":"ok"}"""));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync<Dto>("api/pos/x")));

        Assert.All(results, r => Assert.Equal("ok", r.Value));
        Assert.Equal(1, reauthCalls);
    }

    [Fact]
    public async Task Network_failure_becomes_friendly_error_and_turns_indicator_red_then_green()
    {
        var api = new FakeApi { Respond = _ => throw new HttpRequestException("No connection could be made") };
        var client = api.Client();

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<Dto>("api/pos/x"));
        Assert.Equal(0, ex.StatusCode);
        Assert.Contains("Sunucuya ulasilamadi", ex.Message);
        Assert.False(ConnectivityMonitor.Instance.IsOnline);
        Assert.Equal("Baglanti yok", ConnectivityMonitor.Instance.StatusText);

        api.Respond = _ => Task.FromResult(FakeApi.Json(200, """{"status":"ok"}"""));
        await client.PingAsync();
        Assert.True(ConnectivityMonitor.Instance.IsOnline);
        Assert.Equal("Bagli", ConnectivityMonitor.Instance.StatusText);
    }

    [Fact]
    public async Task Slow_server_times_out_with_friendly_message()
    {
        var api = new FakeApi
        {
            Respond = async _ => { await Task.Delay(TimeSpan.FromSeconds(5)); return FakeApi.Json(200, "{}"); }
        };
        var client = api.Client(TimeSpan.FromMilliseconds(300));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<Dto>("api/pos/x"));

        Assert.Equal(0, ex.StatusCode);
        Assert.Contains("yanit vermedi", ex.Message);
        await client.PingAsync().ContinueWith(_ => { });   // gostergeyi sonraki testler icin toparla
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, "{}"));
        await client.PingAsync();
    }

    [Fact]
    public async Task Server_error_message_is_shown_once_not_duplicated()
    {
        var api = new FakeApi
        {
            Respond = _ => Task.FromResult(FakeApi.Json(400,
                """{"message":"Bir satirda en fazla 999 adet olabilir.","detail":"Bir satirda en fazla 999 adet olabilir."}"""))
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.Client().PostAsync<Dto>("api/pos/sales", new { }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, CountOf(ex.Message, "999 adet"));
    }

    private static int CountOf(string text, string part)
        => (text.Length - text.Replace(part, "").Length) / part.Length;
}
