using System.IO;
using System.Text.Json;
using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Offline;
using LCWaikiki.POS.WpfClient.Core.Services;
using LCWaikiki.POS.WpfClient.ViewModels;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>Çevrimdışı satış: disk kuyruğu, arka plan gönderimi, ödeme ekranı ve yerel katalog.</summary>
[Collection("ApiClient")]
public sealed class OfflineSaleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wpos-offline-test-" + Guid.NewGuid().ToString("N")[..8]);

    public OfflineSaleTests() => ConnectivityMonitor.Instance.Report(true);

    public void Dispose()
    {
        ConnectivityMonitor.Instance.Report(true);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private const string LoginJson =
        """{"token":"t1","fullName":"Test Kasiyer","personelId":"1000001","storeId":7,"isFirstLogin":false,"roles":["Cashier"]}""";

    private static async Task<(FakeApi api, ApiClient client, AuthService auth)> LoggedInAsync()
    {
        var api = new FakeApi { Respond = _ => Task.FromResult(FakeApi.Json(200, LoginJson)) };
        var client = api.Client();
        var auth = new AuthService(client);
        await auth.LoginAsync("1000001", "4826");
        while (api.Requests.TryDequeue(out _)) { }
        return (api, client, auth);
    }

    private OfflineSaleQueue Queue() => new(_dir, terminalNo: 12);

    private static OfflineSale Sale(string reference, string cashier = "1000001", int store = 7, DateTime? soldAt = null) => new()
    {
        Reference = reference, StoreId = store, CashierPersonelId = cashier, CashierName = "K",
        SoldAtUtc = soldAt ?? DateTime.UtcNow, FinalAmount = 100,
        Request = new ProcessSaleRequest([new SaleItemRequest(1, 1, null, null, 100)], [new SalePaymentRequest(0, 100)],
            Offline: new OfflineSaleInfo(reference, soldAt ?? DateTime.UtcNow))
    };

    // ── Kuyruk ───────────────────────────────────────────────────

    [Fact]
    public void Reference_is_18_digits_store_date_terminal_sequence_and_unique()
    {
        var q = Queue();
        var day = new DateTime(2026, 11, 6, 14, 0, 0);
        var a = q.NextReference(7, day);
        var b = q.NextReference(7, day);

        Assert.Equal("9007261106001200" + "01", a);
        Assert.Equal("9007261106001200" + "02", b);
        Assert.Matches("^[0-9]{18}$", a);
        Assert.Equal("900726110700120001", q.NextReference(7, day.AddDays(1)));   // her gün 1'den
    }

    [Fact]
    public void Queue_survives_restart_and_moves_between_states()
    {
        var q = Queue();
        q.Enqueue(Sale("900700000000000001"));
        q.Enqueue(Sale("900700000000000002"));
        Assert.Equal(2, q.PendingCount);

        var reopened = Queue();   // uygulama yeniden açıldı
        Assert.Equal(2, reopened.PendingCount);
        var first = reopened.Pending()[0];

        reopened.MarkSynced(first, "007202611060001");
        reopened.MarkFailed(reopened.Pending()[0], "fiyat geçersiz");
        Assert.Equal(0, reopened.PendingCount);
        Assert.Equal(1, reopened.FailedCount);
        Assert.Equal("007202611060001", reopened.Synced().Single().ServerReceiptNumber);

        var failed = reopened.Failed().Single();
        reopened.RetryFailed(failed);
        Assert.Equal(1, reopened.PendingCount);
        reopened.MarkFailed(reopened.Pending()[0], "tekrar");
        reopened.Dismiss(reopened.Failed()[0], "2000001");
        Assert.Equal(0, reopened.FailedCount);
        Assert.Single(Directory.GetFiles(reopened.DismissedDir, "*.json"));
    }

    // ── Gönderim ─────────────────────────────────────────────────

    [Fact]
    public async Task Sync_sends_oldest_first_with_its_own_key_and_offline_info_then_marks_synced()
    {
        var (api, client, auth) = await LoggedInAsync();
        var q = Queue();
        var older = Sale("900700000000000001", soldAt: DateTime.UtcNow.AddMinutes(-10));
        var newer = Sale("900700000000000002", soldAt: DateTime.UtcNow.AddMinutes(-5));
        q.Enqueue(newer);
        q.Enqueue(older);
        var n = 0;
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, FakeApi.SaleOk($"00720261106000{Interlocked.Increment(ref n)}")));

        var sync = new OfflineSyncService(client, auth, q, new OfflineCatalog(_dir), new ClientSettings());
        var result = await sync.SyncNowAsync();

        Assert.Equal(2, result.Sent);
        var sent = api.Requests.ToArray();
        Assert.Equal([older.Id, newer.Id], sent.Select(r => r.IdempotencyKey));
        using var body = JsonDocument.Parse(sent[0].Body);
        Assert.Equal(older.Reference, body.RootElement.GetProperty("Offline").GetProperty("Reference").GetString());
        Assert.Equal(100m, body.RootElement.GetProperty("Items")[0].GetProperty("OfflineUnitPrice").GetDecimal());
        Assert.Equal(0, q.PendingCount);
        Assert.Equal(["007202611060001", "007202611060002"], q.Synced().Select(s => s.ServerReceiptNumber));
    }

    [Fact]
    public async Task Another_cashiers_sale_waits_for_that_cashier()
    {
        var (api, client, auth) = await LoggedInAsync();
        var q = Queue();
        q.Enqueue(Sale("900700000000000001", cashier: "1000099"));
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, FakeApi.SaleOk()));

        await new OfflineSyncService(client, auth, q, new OfflineCatalog(_dir), new ClientSettings()).SyncNowAsync();

        Assert.Empty(api.Requests);
        Assert.Equal(1, q.PendingCount);
    }

    [Fact]
    public async Task Network_error_or_expired_session_keeps_sales_pending_without_pin_popup()
    {
        var (api, client, auth) = await LoggedInAsync();
        var reauthAsked = 0;
        client.ReauthenticateAsync = () => { reauthAsked++; return Task.FromResult(true); };
        var q = Queue();
        q.Enqueue(Sale("900700000000000001", soldAt: DateTime.UtcNow.AddMinutes(-2)));
        q.Enqueue(Sale("900700000000000002", soldAt: DateTime.UtcNow.AddMinutes(-1)));
        var sync = new OfflineSyncService(client, auth, q, new OfflineCatalog(_dir), new ClientSettings());

        api.Respond = _ => throw new HttpRequestException("ağ yok");
        var r1 = await sync.SyncNowAsync();
        Assert.Equal(0, r1.Sent);
        Assert.Single(api.Requests);                     // ilk hatada durdu, sırayı korudu
        Assert.Equal(2, q.PendingCount);
        Assert.Equal(1, q.Pending()[0].Attempts);

        api.Respond = _ => Task.FromResult(FakeApi.Expired());
        await sync.SyncNowAsync();
        Assert.Equal(0, reauthAsked);                    // arka planda PIN penceresi açılmaz
        Assert.Equal(2, q.PendingCount);
    }

    [Fact]
    public async Task Rejected_sale_moves_to_failed_and_the_rest_continue()
    {
        var (api, client, auth) = await LoggedInAsync();
        var q = Queue();
        q.Enqueue(Sale("900700000000000001", soldAt: DateTime.UtcNow.AddMinutes(-2)));
        q.Enqueue(Sale("900700000000000002", soldAt: DateTime.UtcNow.AddMinutes(-1)));
        api.Respond = r => Task.FromResult(r.Body.Contains("900700000000000001")
            ? FakeApi.Json(400, """{"message":"fiyat geçerli değildi"}""")
            : FakeApi.Json(200, FakeApi.SaleOk()));

        var result = await new OfflineSyncService(client, auth, q, new OfflineCatalog(_dir), new ClientSettings()).SyncNowAsync();

        Assert.Equal((1, 1), (result.Sent, result.Failed));
        Assert.Contains("fiyat geçerli değildi", q.Failed().Single().LastError);
    }

    // ── Ödeme ekranı ─────────────────────────────────────────────

    private (PaymentViewModel vm, OfflineSaleQueue q) Payment(ApiClient client, AuthService auth, decimal total)
    {
        var q = Queue();
        var vm = new PaymentViewModel(new PosService(client), auth, new ClientSettings(), q);
        vm.ResetPaymentState();
        vm.SaleItems = [new SaleItemRequest(5, 2, null, null, total / 2)];
        vm.GrandTotal = total;
        return (vm, q);
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
    }

    [Fact]
    public async Task Network_failure_saves_sale_offline_with_same_key_and_prints_offline_number()
    {
        var (api, client, auth) = await LoggedInAsync();
        api.Respond = _ => throw new HttpRequestException("ağ koptu");
        var (vm, q) = Payment(client, auth, 200m);
        ProcessSaleResponse? completed = null;
        vm.SaleCompleted += r => completed = r;

        foreach (var c in "200") vm.AppendDigitCommand.Execute(c.ToString());
        vm.AddCashCommand.Execute(null);
        await WaitFor(() => completed is not null);

        Assert.NotNull(completed);
        Assert.True(completed!.IsOffline);
        var queued = q.Pending().Single();
        Assert.Equal(vm.IdempotencyKey, queued.Id);                 // ilk denemeyle aynı anahtar
        Assert.Equal(completed.ReceiptNumber, queued.Reference);
        Assert.Equal(queued.Reference, queued.Request.Offline!.Reference);
        Assert.Equal(100m, queued.Request.Items.Single().OfflineUnitPrice);
        Assert.Equal("1000001", queued.CashierPersonelId);
        Assert.Equal(200m, queued.FinalAmount);
    }

    [Fact]
    public async Task When_already_offline_the_sale_is_queued_without_waiting_for_a_timeout()
    {
        var (api, client, auth) = await LoggedInAsync();
        ConnectivityMonitor.Instance.Report(false);
        var (vm, q) = Payment(client, auth, 100m);
        ProcessSaleResponse? completed = null;
        vm.SaleCompleted += r => completed = r;

        foreach (var c in "100") vm.AppendDigitCommand.Execute(c.ToString());
        vm.AddCashCommand.Execute(null);
        await WaitFor(() => completed is not null);

        Assert.True(completed!.IsOffline);
        Assert.Empty(api.Requests);
        Assert.Equal(1, q.PendingCount);
    }

    [Fact]
    public async Task Gift_card_payment_cannot_be_completed_offline()
    {
        var (api, client, auth) = await LoggedInAsync();
        api.Respond = _ => throw new HttpRequestException("ağ koptu");
        var (vm, q) = Payment(client, auth, 100m);
        vm.PaymentLines.Add(new PaymentLine { Label = "Hediye", Amount = 100, Type = PaymentType.GiftCard, CardNumber = "HK1" });
        await WaitFor(() => !string.IsNullOrEmpty(vm.ErrorMessage));

        Assert.Contains("hediye karti", vm.ErrorMessage);
        Assert.Equal(0, q.PendingCount);
    }

    // ── Katalog ──────────────────────────────────────────────────

    [Fact]
    public async Task Catalog_is_downloaded_saved_and_used_when_server_is_unreachable()
    {
        var (api, client, _) = await LoggedInAsync();
        api.Respond = _ => Task.FromResult(FakeApi.Json(200, """
            {"generatedAtUtc":"2026-11-06T08:00:00Z","items":[
              {"variantId":5,"productId":2,"barcode":"8684001001","productCode":"W6ER01Z8","name":"Slim Fit Pantolon",
               "color":"Siyah","size":"M","categoryName":"Erkek","price":499.9,"imagePath":null}]}
            """));
        var catalog = new OfflineCatalog(_dir);
        await catalog.RefreshAsync(client, 7);

        var reloaded = new OfflineCatalog(_dir);   // uygulama yeniden açıldı (çevrimdışı)
        reloaded.Load(7);
        Assert.Equal(1, reloaded.Count);

        api.Respond = _ => throw new HttpRequestException("ağ yok");
        var products = new ProductService(client, reloaded);
        var p = await products.GetByBarcodeAsync("8684001001");
        Assert.True(p.FromOfflineCatalog);
        Assert.Equal(499.9m, p.CurrentPrice);
        Assert.Equal(5, p.VariantId);

        var ex = await Assert.ThrowsAsync<ApiException>(() => products.GetByBarcodeAsync("0000000000"));
        Assert.Contains("cevrimdisi katalogda yok", ex.Message);
    }
}
