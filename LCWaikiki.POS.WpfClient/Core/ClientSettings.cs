using System.IO;
using System.Text.Json;

namespace LCWaikiki.POS.WpfClient.Core;

/// <summary>
/// Uygulama dizinindeki appsettings.json'dan okunan istemci ayarlari.
/// Dosya yoksa ya da bir alan eksikse asagidaki varsayilanlar kullanilir.
/// </summary>
public class ClientSettings
{
    public ApiSettings Api { get; set; } = new();
    public CurrencySettings Currency { get; set; } = new();

    /// <summary>Kasa cekmecesi: fis yazicisina bagli ise yazici adi (bos = cekmece yok).</summary>
    public CashDrawerSettings CashDrawer { get; set; } = new();

    /// <summary>"Hizli Urunler" paneli: barkodu olmayan/sik satilan urunler (poset vb.).</summary>
    public List<QuickProductSetting> QuickProducts { get; set; } = new();

    /// <summary>"Ulke" secimi listesi (turist / yurt disi musteri).</summary>
    public List<string> Countries { get; set; } = new()
    {
        "Turkiye", "Almanya", "Azerbaycan", "Irak", "Iran", "Rusya", "Ukrayna",
        "Kazakistan", "Gurcistan", "Suudi Arabistan", "Ingiltere", "Diger"
    };

    public class CashDrawerSettings
    {
        /// <summary>Windows yazici adi (Ayarlar > Yazicilar). Cekmece bu yaziciya ESC/POS darbesiyle acilir.</summary>
        public string? PrinterName { get; set; }
    }

    public class QuickProductSetting
    {
        public string Label { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "https://localhost:7164";

        /// <summary>
        /// Gelistirme ortami icin: true ise SADECE localhost adreslerinde
        /// guvenilmeyen (self-signed) sertifika kabul edilir. Uzak bir
        /// sunucuda bu ayar true olsa bile sertifika her zaman dogrulanir.
        /// </summary>
        public bool AllowUntrustedLocalhostCertificate { get; set; }

        /// <summary>Istek zaman asimi (sn). Ag takilinca kasa en fazla bu kadar bekler.</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Baglanti gostergesi yoklama araligi (sn).</summary>
        public int HealthCheckSeconds { get; set; } = 15;
    }

    public class CurrencySettings
    {
        public decimal UsdRate { get; set; } = 32.50m;
        public decimal EurRate { get; set; } = 35.20m;
    }

    public static ClientSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new ClientSettings();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ClientSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new ClientSettings();
    }
}
