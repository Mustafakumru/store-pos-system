using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;

namespace LCWaikiki.POS.WpfClient.Core.Offline;

public record OfflineSyncResult(int Sent, int Failed, int Remaining, string? StoppedBecause);

/// <summary>
/// Bekleyen çevrimdışı satışları sunucuya gönderir ve kataloğu tazeler.
///  • En eski satış önce; her satış KENDİ Idempotency-Key'iyle (tekrar gönderim güvenli).
///  • Satış yalnızca onu yapan kasiyerin oturumuyla gönderilir (fiş başka kasiyere yazılmasın).
///  • Ağ hatası / sunucu meşgul (409) / oturum süresi doldu → durur, sonra tekrar dener.
///  • Sunucu kuralı reddederse (400, 404, 422) satış "reddedildi" klasörüne taşınır.
/// Arka planda çalışır; PIN penceresi açmaz (oturum yenilemeyi kasiyer işlemi tetikler).
/// </summary>
public class OfflineSyncService
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private readonly OfflineSaleQueue _queue;
    private readonly OfflineCatalog _catalog;
    private readonly ClientSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private CancellationTokenSource? _loop;

    public OfflineSyncService(ApiClient api, AuthService auth, OfflineSaleQueue queue, OfflineCatalog catalog, ClientSettings settings)
    {
        _api = api;
        _auth = auth;
        _queue = queue;
        _catalog = catalog;
        _settings = settings;
    }

    public void Start()
    {
        if (_loop is not null || !_settings.Offline.Enabled) return;
        _loop = new CancellationTokenSource();
        var interval = TimeSpan.FromSeconds(Math.Max(5, _settings.Offline.SyncIntervalSeconds));
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            do
            {
                try { await TickAsync(_loop.Token); }
                catch (Exception ex) when (ex is not OperationCanceledException) { AppLog.Error("Cevrimdisi senkron hatasi", ex); }
            } while (await timer.WaitForNextTickAsync(_loop.Token));
        });
    }

    public void Stop() => _loop?.Cancel();

    /// <summary>Periyodik iş: katalog tazeleme (gerekirse) + bekleyen satışları gönderme.</summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null || !_api.HasToken) return;
        // Giriş anında hem döngü hem anlık tetik çalışabilir: aynı anda tek tur
        // (iki katalog yenilemesi aynı geçici dosyaya yazmaya çalışıyordu).
        if (!await _tickGate.WaitAsync(0, ct)) return;
        try
        {
            await TickCoreAsync(user, ct);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    private async Task TickCoreAsync(Models.Auth.LoginResponse user, CancellationToken ct)
    {
        if (_catalog.NeedsRefresh(TimeSpan.FromMinutes(Math.Max(5, _settings.Offline.CatalogRefreshMinutes))))
        {
            try { await _catalog.RefreshAsync(_api, user.StoreId, ct); }
            catch (ApiException ex) { AppLog.Warn($"Cevrimdisi katalog guncellenemedi: {ex.Message}"); }
        }
        if (_queue.PendingCount > 0)
            await SyncNowAsync(ct);
    }

    public async Task<OfflineSyncResult> SyncNowAsync(CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null) return new(0, 0, _queue.PendingCount, "Oturum yok");
        if (!await _gate.WaitAsync(0, ct)) return new(0, 0, _queue.PendingCount, "Gönderim zaten sürüyor");
        int sent = 0, failed = 0;
        string? stopped = null;
        try
        {
            foreach (var sale in _queue.Pending())
            {
                if (sale.StoreId != user.StoreId || sale.CashierPersonelId != user.PersonelId)
                    continue;   // başka kasiyerin satışı: o kasiyer giriş yapınca gönderilir

                try
                {
                    var result = await _api.PostAsync<ProcessSaleResponse>(
                        "api/pos/sales", sale.Request, sale.Id, allowReauth: false, ct);
                    _queue.MarkSynced(sale, result.ReceiptNumber);
                    sent++;
                }
                catch (ApiException ex) when (ex.StatusCode is 0 or 401 or 409 or 429 or >= 500)
                {
                    _queue.RecordAttempt(sale, ex.Message);
                    stopped = ex.Message;
                    break;   // bağlantı/oturum sorunu: sırayı bozmadan sonra tekrar dene
                }
                catch (ApiException ex)
                {
                    _queue.MarkFailed(sale, ex.Message);
                    failed++;
                }
            }
        }
        finally
        {
            _queue.RefreshCounts();
            _gate.Release();
        }
        return new(sent, failed, _queue.PendingCount, stopped);
    }

    /// <summary>Bu kasada gönderilmemiş satış var mı (kasa kapanışı / Z raporu öncesi kontrol).</summary>
    public bool HasUnsentSales => _queue.PendingCount > 0;
}
