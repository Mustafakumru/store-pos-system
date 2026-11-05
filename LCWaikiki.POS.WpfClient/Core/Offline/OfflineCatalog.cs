using System.IO;
using System.Text.Json;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Core.Offline;

public record OfflineCatalogItem(
    int VariantId, int ProductId, string Barcode, string? ProductCode, string Name,
    string? Color, string? Size, string CategoryName, decimal Price, string? ImagePath);

public record OfflineCatalogSnapshot(DateTime GeneratedAtUtc, List<OfflineCatalogItem> Items);

/// <summary>
/// Mağazanın satılabilir ürün/fiyat listesinin yerel kopyası. Sunucuya
/// ulaşılamadığında barkod okutma buradan çalışır. Girişte ve periyodik olarak
/// yenilenir; diskte saklanır (uygulama yeniden açılsa da kalır).
/// </summary>
public class OfflineCatalog
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly string _directory;
    private readonly object _gate = new();
    private Dictionary<string, OfflineCatalogItem> _byBarcode = new(StringComparer.OrdinalIgnoreCase);
    private int _storeId;

    public OfflineCatalog(string directory) => _directory = directory;

    public DateTime? GeneratedAtUtc { get; private set; }
    public int Count { get { lock (_gate) return _byBarcode.Count; } }
    public DateTime? LastRefreshAttemptUtc { get; private set; }

    private string FileFor(int storeId) => Path.Combine(_directory, $"catalog-{storeId}.json");

    /// <summary>Diskteki kataloğu yükler (giriş sonrası, çevrimdışı açılış için).</summary>
    public void Load(int storeId)
    {
        _storeId = storeId;
        try
        {
            var path = FileFor(storeId);
            if (!File.Exists(path)) { Apply(null); return; }
            Apply(JsonSerializer.Deserialize<OfflineCatalogSnapshot>(File.ReadAllText(path), Json));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Cevrimdisi katalog okunamadi", ex);
            Apply(null);
        }
    }

    /// <summary>Sunucudan güncel kataloğu indirip diske yazar.</summary>
    public async Task RefreshAsync(ApiClient api, int storeId, CancellationToken ct = default)
    {
        LastRefreshAttemptUtc = DateTime.UtcNow;
        var snapshot = await api.GetAsync<OfflineCatalogSnapshot>("api/pos/offline-catalog", ct);
        Directory.CreateDirectory(_directory);
        lock (_gate) OfflineFile.WriteAtomic(FileFor(storeId), JsonSerializer.Serialize(snapshot));
        _storeId = storeId;
        Apply(snapshot);
        AppLog.Info($"Cevrimdisi katalog guncellendi: {snapshot.Items.Count} urun.");
    }

    public bool NeedsRefresh(TimeSpan interval)
        => LastRefreshAttemptUtc is null || DateTime.UtcNow - LastRefreshAttemptUtc > interval;

    public OfflineCatalogItem? Find(string barcode)
    {
        lock (_gate) return _byBarcode.TryGetValue(barcode.Trim(), out var item) ? item : null;
    }

    /// <summary>Kasa ekranının beklediği ürün bilgisine çevirir (stok bilinmiyor).</summary>
    public ProductForPosDto? FindForPos(string barcode)
    {
        var i = Find(barcode);
        return i is null ? null : new ProductForPosDto(
            i.ProductId, i.VariantId, i.Barcode, i.Name, i.Color, i.Size, i.ImagePath, i.CategoryName,
            i.Price, StockQuantity: 0, IsLowStock: false, IsOutOfStock: false, IsNegativeStock: false,
            ProductCode: i.ProductCode, FromOfflineCatalog: true);
    }

    private void Apply(OfflineCatalogSnapshot? snapshot)
    {
        lock (_gate)
        {
            GeneratedAtUtc = snapshot?.GeneratedAtUtc;
            _byBarcode = (snapshot?.Items ?? [])
                .GroupBy(i => i.Barcode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
    }
}

internal static class OfflineFile
{
    /// <summary>Önce geçici dosyaya yazar, sonra yerine taşır: elektrik kesilse de yarım dosya kalmaz.</summary>
    public static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
