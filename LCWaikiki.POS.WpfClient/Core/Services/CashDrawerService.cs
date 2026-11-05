using System.Runtime.InteropServices;

namespace LCWaikiki.POS.WpfClient.Core.Services;

/// <summary>
/// Kasa cekmecesini acar. Cekmeceler fis yazicisinin RJ11 portuna baglidir ve
/// yaziciya gonderilen ESC/POS "darbe" komutuyla (ESC p 0 25 250) acilir.
/// Yazici adi appsettings.json > CashDrawer:PrinterName'den okunur.
/// </summary>
public class CashDrawerService(ClientSettings settings)
{
    private readonly ClientSettings _settings = settings;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.CashDrawer.PrinterName);

    /// <summary>Cekmeceyi acar; basarisizsa aciklayici bir hata mesaji doner (basariliysa null).</summary>
    public string? Open()
    {
        if (!IsConfigured)
            return "Kasa cekmecesi tanimli degil. appsettings.json > CashDrawer:PrinterName alanina " +
                   "cekmecenin bagli oldugu fis yazicisinin adini yazin.";

        byte[] pulse = [0x1B, 0x70, 0x00, 0x19, 0xFA]; // ESC p m t1 t2
        var printer = _settings.CashDrawer.PrinterName!;
        try
        {
            return RawPrinter.Send(printer, pulse)
                ? null
                : $"'{printer}' yazicisina cekmece komutu gonderilemedi (Win32 hata {Marshal.GetLastWin32Error()}).";
        }
        catch (Exception ex)
        {
            return $"Cekmece acilamadi: {ex.Message}";
        }
    }

    private static class RawPrinter
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DocInfo
        {
            public string DocName = "WPOS Cekmece";
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

        public static bool Send(string printer, byte[] data)
        {
            if (!OpenPrinter(printer, out var h, IntPtr.Zero)) return false;
            try
            {
                if (StartDocPrinter(h, 1, new DocInfo()) == 0) return false;
                try
                {
                    if (!StartPagePrinter(h)) return false;
                    var ok = WritePrinter(h, data, data.Length, out var written) && written == data.Length;
                    EndPagePrinter(h);
                    return ok;
                }
                finally { EndDocPrinter(h); }
            }
            finally { ClosePrinter(h); }
        }
    }
}
