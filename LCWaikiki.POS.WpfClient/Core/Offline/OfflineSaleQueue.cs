using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Offline;

/// <summary>Diskte bekleyen çevrimdışı satış.</summary>
public sealed class OfflineSale
{
    /// <summary>Idempotency-Key: aynı satış kaç kez gönderilirse gönderilsin sunucuda tek kayıt.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Reference { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string CashierPersonelId { get; set; } = string.Empty;
    public string? CashierName { get; set; }
    public DateTime SoldAtUtc { get; set; }
    public decimal FinalAmount { get; set; }
    public ProcessSaleRequest Request { get; set; } = null!;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public string? ServerReceiptNumber { get; set; }
    public string? DismissedBy { get; set; }
    public DateTime? DismissedAtUtc { get; set; }
}

/// <summary>
/// Çevrimdışı satış kuyruğu: her satış ayrı bir JSON dosyası.
///   pending/  gönderilmeyi bekleyen
///   synced/   sunucuya kaydedilmiş (fiş no ile; kayıt için tutulur)
///   failed/   sunucunun reddettiği (mağaza müdürü incelemeli)
/// Ekranlardaki "bekleyen satış" sayacı PendingCount / FailedCount'a bağlıdır.
/// </summary>
public partial class OfflineSaleQueue : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly object FileGate = new();
    private readonly string _root;
    private readonly int _terminalNo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending), nameof(StatusText))]
    private int _pendingCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed), nameof(StatusText))]
    private int _failedCount;

    public bool HasPending => PendingCount > 0;
    public bool HasFailed => FailedCount > 0;
    public string StatusText => FailedCount > 0
        ? $"{PendingCount} satış gönderilmeyi bekliyor · {FailedCount} satış reddedildi"
        : $"{PendingCount} satış gönderilmeyi bekliyor";

    /// <summary>Uygulamanın kullandığı kuyruk (XAML: {x:Static offline:OfflineSaleQueue.Current}).</summary>
    public static OfflineSaleQueue Current { get; set; } = new(DefaultRoot, 0);

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LCWaikiki", "WPOS", "Offline");

    public OfflineSaleQueue(string root, int terminalNo)
    {
        _root = root;
        _terminalNo = terminalNo is > 0 and < 10000 ? terminalNo : MachineTerminalNo();
        foreach (var d in new[] { PendingDir, SyncedDir, FailedDir, DismissedDir }) Directory.CreateDirectory(d);
        RefreshCounts();
    }

    public string PendingDir => Path.Combine(_root, "pending");
    public string SyncedDir => Path.Combine(_root, "synced");
    public string FailedDir => Path.Combine(_root, "failed");
    public string DismissedDir => Path.Combine(_root, "dismissed");
    public int TerminalNo => _terminalNo;

    /// <summary>
    /// Fişe basılan çevrimdışı belge no (yalnızca rakam, barkod olarak okunabilir, 18 hane):
    /// 9 + mağaza(3) + yyMMdd + terminal(4) + sıra(4). Normal fiş no 15 hanedir; karışmaz.
    /// </summary>
    public string NextReference(int storeId, DateTime localNow)
    {
        lock (FileGate)
        {
            var day = localNow.ToString("yyMMdd");
            var counterFile = Path.Combine(_root, $"seq-{day}.txt");
            var next = File.Exists(counterFile) && int.TryParse(File.ReadAllText(counterFile), out var n) ? n + 1 : 1;
            OfflineFile.WriteAtomic(counterFile, next.ToString());
            return $"9{storeId % 1000:000}{day}{_terminalNo:0000}{next % 10000:0000}";
        }
    }

    public void Enqueue(OfflineSale sale)
    {
        lock (FileGate) OfflineFile.WriteAtomic(PathFor(PendingDir, sale), JsonSerializer.Serialize(sale));
        AppLog.Warn($"Cevrimdisi satis kaydedildi: {sale.Reference} ({sale.FinalAmount:N2} TL).");
        RefreshCounts();
    }

    /// <summary>Bekleyenler, en eski satış önce (gönderim sırası).</summary>
    public List<OfflineSale> Pending() => ReadAll(PendingDir).OrderBy(s => s.SoldAtUtc).ToList();
    public List<OfflineSale> Failed() => ReadAll(FailedDir).OrderBy(s => s.SoldAtUtc).ToList();
    public List<OfflineSale> Synced() => ReadAll(SyncedDir).OrderBy(s => s.SoldAtUtc).ToList();

    public void MarkSynced(OfflineSale sale, string receiptNumber)
    {
        sale.SyncedAtUtc = DateTime.UtcNow;
        sale.ServerReceiptNumber = receiptNumber;
        Move(sale, PendingDir, SyncedDir);
        AppLog.Info($"Cevrimdisi satis gonderildi: {sale.Reference} -> fis {receiptNumber}.");
    }

    public void MarkFailed(OfflineSale sale, string error)
    {
        sale.LastError = error;
        Move(sale, PendingDir, FailedDir);
        AppLog.Error($"Cevrimdisi satis sunucuda reddedildi: {sale.Reference} - {error}");
    }

    /// <summary>Reddedilen satışı (örn. fiyat düzeltildikten sonra) yeniden gönderim sırasına alır.</summary>
    public void RetryFailed(OfflineSale sale)
    {
        sale.LastError = null;
        Move(sale, FailedDir, PendingDir);
        AppLog.Info($"Reddedilen cevrimdisi satis yeniden kuyruga alindi: {sale.Reference}.");
    }

    /// <summary>
    /// Reddedilen satışı yönetici onayıyla arşivler (sunucuya gönderilmez; satış elle
    /// kaydedildi ya da iptal edildi). Dosya silinmez, "dismissed" klasöründe kalır.
    /// </summary>
    public void Dismiss(OfflineSale sale, string approvedBy)
    {
        sale.DismissedBy = approvedBy;
        sale.DismissedAtUtc = DateTime.UtcNow;
        Move(sale, FailedDir, DismissedDir);
        AppLog.Warn($"Reddedilen cevrimdisi satis arsivlendi: {sale.Reference} ({sale.FinalAmount:N2} TL), onaylayan {approvedBy}.");
    }

    public void RecordAttempt(OfflineSale sale, string? error)
    {
        sale.Attempts++;
        sale.LastError = error;
        lock (FileGate)
        {
            var path = PathFor(PendingDir, sale);
            if (File.Exists(path)) OfflineFile.WriteAtomic(path, JsonSerializer.Serialize(sale));
        }
    }

    public void RefreshCounts()
    {
        var pending = Directory.GetFiles(PendingDir, "*.json").Length;
        var failed = Directory.GetFiles(FailedDir, "*.json").Length;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) { PendingCount = pending; FailedCount = failed; }
        else dispatcher.BeginInvoke(() => { PendingCount = pending; FailedCount = failed; });
    }

    private void Move(OfflineSale sale, string from, string to)
    {
        lock (FileGate)
        {
            OfflineFile.WriteAtomic(PathFor(to, sale), JsonSerializer.Serialize(sale));
            var src = PathFor(from, sale);
            if (File.Exists(src)) File.Delete(src);
        }
        RefreshCounts();
    }

    private static string PathFor(string dir, OfflineSale sale) => Path.Combine(dir, $"{sale.Reference}_{sale.Id}.json");

    private static List<OfflineSale> ReadAll(string dir)
    {
        var list = new List<OfflineSale>();
        lock (FileGate)
        {
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var sale = JsonSerializer.Deserialize<OfflineSale>(File.ReadAllText(f), Json);
                    if (sale is not null) list.Add(sale);
                }
                catch (Exception ex) { AppLog.Error($"Cevrimdisi satis dosyasi okunamadi: {f}", ex); }
            }
        }
        return list;
    }

    /// <summary>Ayarda terminal no yoksa makine adından kararlı 4 haneli numara.</summary>
    private static int MachineTerminalNo()
    {
        uint hash = 2166136261;
        foreach (var c in Environment.MachineName.ToUpperInvariant()) hash = (hash ^ c) * 16777619;
        return (int)(hash % 9000) + 1000;
    }
}
