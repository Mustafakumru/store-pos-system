using System.IO;
using System.Runtime.InteropServices;

namespace LCWaikiki.POS.WpfClient.Core.Printing;

/// <summary>
/// Windows yazıcı kuyruğuna ham bayt gönderir (sürücü işlemez: ESC/POS komutları
/// olduğu gibi yazıcıya gider). Fiş yazıcısı ve kasa çekmecesi kullanır.
/// "file:" ile başlayan ad, baytları dosyaya yazar (tanılama ve testler için).
/// </summary>
public static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DocInfo
    {
        public string DocName = "WPOS";
        public string? OutputFile;
        public string DataType = "RAW";
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr handle, int level, [In] DocInfo info);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, byte[] data, int count, out int written);

    /// <summary>Başarılıysa null, değilse açıklayıcı hata mesajı.</summary>
    public static string? Send(string printer, byte[] data, string documentName = "WPOS")
    {
        if (printer.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var path = printer[5..];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, data);
            return null;
        }

        if (!OpenPrinter(printer, out var h, IntPtr.Zero))
            return $"'{printer}' yazıcısı bulunamadı (Win32 hata {Marshal.GetLastWin32Error()}). Yazıcı adını kontrol edin.";
        try
        {
            if (StartDocPrinter(h, 1, new DocInfo { DocName = documentName }) == 0)
                return $"'{printer}' yazıcısında iş başlatılamadı (Win32 hata {Marshal.GetLastWin32Error()}).";
            try
            {
                if (!StartPagePrinter(h)) return $"'{printer}' sayfa başlatılamadı (Win32 hata {Marshal.GetLastWin32Error()}).";
                var ok = WritePrinter(h, data, data.Length, out var written) && written == data.Length;
                EndPagePrinter(h);
                return ok ? null : $"'{printer}' yazıcısına veri gönderilemedi (Win32 hata {Marshal.GetLastWin32Error()}).";
            }
            finally { EndDocPrinter(h); }
        }
        finally { ClosePrinter(h); }
    }
}
