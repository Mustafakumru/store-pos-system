using System.IO;
using System.Text;

namespace LCWaikiki.POS.WpfClient.Core;

/// <summary>
/// Kasa uygulamasinin gunluk log dosyasi:
///   %ProgramData%\LCWaikiki\WPOS\Logs\wpos-YYYYMMDD.log
/// Hata, API cagrisi (yol + durum + sure) ve onemli olaylar yazilir. Musteri
/// verisi (PIN, kart no, istek govdesi) YAZILMAZ. 30 gunden eski dosyalar silinir.
/// Log yazilamazsa uygulama asla etkilenmez (sessizce gecer).
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private const int KeepDays = 30;

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "LCWaikiki", "WPOS", "Logs");

    public static string CurrentFile => Path.Combine(Directory, $"wpos-{DateTime.Now:yyyyMMdd}.log");

    /// <summary>Kasiyer/kasa bilgisi (giris yapilinca set edilir) - her satira eklenir.</summary>
    public static string? Context { get; set; }

    public static void Info(string message) => Write("INFO ", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN ", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public static void Initialize()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            foreach (var f in System.IO.Directory.GetFiles(Directory, "wpos-*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-KeepDays))
                    File.Delete(f);
        }
        catch { /* log altyapisi uygulamayi asla durdurmaz */ }
        Info($"Uygulama basladi. Surum {typeof(AppLog).Assembly.GetName().Version}, makine {Environment.MachineName}");
    }

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ')
                .Append(level).Append(' ');
            if (!string.IsNullOrEmpty(Context)) sb.Append('[').Append(Context).Append("] ");
            sb.Append(message);
            if (ex is not null) sb.AppendLine().Append(ex);
            sb.AppendLine();
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(CurrentFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { /* yoksay */ }
    }
}
