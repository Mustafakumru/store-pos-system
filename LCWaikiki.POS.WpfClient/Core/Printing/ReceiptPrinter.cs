using System.Printing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LCWaikiki.POS.WpfClient.Core.Printing;

/// <summary>Son yazdırmanın durumu (fiş ekranındaki "Yazıcı" uyarısı buna bağlı).</summary>
public partial class PrinterStatus : ObservableObject
{
    public static PrinterStatus Instance { get; } = new();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))] private string? _lastError;
    public bool HasError => !string.IsNullOrEmpty(LastError);

    internal void Report(string? error)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) LastError = error;
        else dispatcher.BeginInvoke(() => LastError = error);
    }
}

/// <summary>
/// Fişi termal yazıcıya basar. İki yöntem (appsettings.json > Printer:Mode):
///  • "EscPos" — fiş görseli yazıcı çözünürlüğünde (203 dpi, 80 mm = 576 nokta) siyah-beyaz
///    resme çevrilip ham ESC/POS ile gönderilir; ardından kağıt kesilir. Hızlıdır, Türkçe
///    karakter sorunu yoktur. Yazıcı Windows'ta "Generic / Text Only" ya da üreticinin
///    sürücüsüyle kurulu olabilir (ham veri sürücüden geçmeden gider).
///  • "Driver" — aynı görsel Windows yazıcı sürücüsüyle, kağıt genişliğine ölçeklenerek basılır
///    (ESC/POS desteklemeyen ya da sürücüsü zorunlu yazıcılar için). Kesme sürücü ayarıdır.
/// Yazdırma hatası satışı ASLA bozmaz: hata loglanır ve fiş ekranında gösterilir; fiş PDF olarak
/// zaten kayıtlıdır ve "Son Belge Tekrarı" ile yeniden basılabilir.
/// </summary>
public class ReceiptPrinter(ClientSettings settings)
{
    private readonly ClientSettings.PrinterSettings _cfg = settings.Printer;

    public bool IsEnabled => _cfg.Enabled && !string.IsNullOrWhiteSpace(_cfg.Name);

    /// <summary>Fiş görselini basar (UI thread'de çağrılmalı). Başarılıysa null, değilse hata mesajı.</summary>
    public string? Print(FrameworkElement receipt, double contentWidthDiu, string documentName, bool kickDrawer = false)
    {
        if (!IsEnabled) return null;
        string? error;
        try
        {
            error = string.Equals(_cfg.Mode, "Driver", StringComparison.OrdinalIgnoreCase)
                ? PrintWithDriver(receipt, contentWidthDiu, documentName)
                : RawPrinter.Send(_cfg.Name!, BuildEscPosJob(receipt, contentWidthDiu, kickDrawer), documentName);
        }
        catch (Exception ex)
        {
            error = $"Yazdırılamadı: {ex.Message}";
        }

        if (error is null) AppLog.Info($"Fis yazdirildi: {documentName} ({_cfg.Mode}, {_cfg.Name}).");
        else AppLog.Error($"Fis yazdirilamadi: {documentName} - {error}");
        PrinterStatus.Instance.Report(error is null ? null : $"Yazıcı: {error}");
        return error;
    }

    /// <summary>ESC/POS işi (testlerde de kullanılır).</summary>
    public byte[] BuildEscPosJob(FrameworkElement receipt, double contentWidthDiu, bool kickDrawer)
    {
        var dots = _cfg.DotsPerLine > 0 ? _cfg.DotsPerLine : EscPos.DotsForPaper(_cfg.PaperWidthMm);
        var (bgra, width, height, stride) = Rasterize(receipt, contentWidthDiu, dots);
        var packed = EscPos.ToMonochrome(bgra, width, height, stride, _cfg.Threshold);
        return EscPos.ReceiptJob(packed, width, height, _cfg.AutoCut, _cfg.FeedLinesBeforeCut, kickDrawer && _cfg.OpenDrawerOnCashSale);
    }

    /// <summary>Görseli tam olarak <paramref name="widthDots"/> nokta genişliğinde BGRA bitmap'e çizer.</summary>
    public static (byte[] Bgra, int Width, int Height, int Stride) Rasterize(FrameworkElement element, double contentWidthDiu, int widthDots)
    {
        element.Measure(new Size(contentWidthDiu, double.PositiveInfinity));
        element.Arrange(new Rect(0, 0, contentWidthDiu, element.DesiredSize.Height));
        element.UpdateLayout();

        var dpi = 96.0 * widthDots / contentWidthDiu;
        var height = (int)Math.Ceiling(element.DesiredSize.Height * dpi / 96.0);
        var rtb = new RenderTargetBitmap(widthDots, height, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(element);

        var stride = widthDots * 4;
        var pixels = new byte[stride * height];
        rtb.CopyPixels(pixels, stride, 0);
        return (pixels, widthDots, height, stride);
    }

    private string? PrintWithDriver(FrameworkElement receipt, double contentWidthDiu, string documentName)
    {
        PrintQueue queue;
        try
        {
            queue = new LocalPrintServer().GetPrintQueue(_cfg.Name!);
        }
        catch (Exception ex)
        {
            return $"'{_cfg.Name}' yazıcısı bulunamadı: {ex.Message}";
        }

        // Kağıt genişliği (yazdırılabilir alan ~ kağıt - 8 mm) için ölçekle
        var paperDiu = _cfg.PaperWidthMm / 25.4 * 96.0;
        var printableDiu = (_cfg.PaperWidthMm - 8) / 25.4 * 96.0;
        var scale = printableDiu / contentWidthDiu;

        receipt.Measure(new Size(contentWidthDiu, double.PositiveInfinity));
        receipt.Arrange(new Rect(0, 0, contentWidthDiu, receipt.DesiredSize.Height));
        receipt.LayoutTransform = new ScaleTransform(scale, scale);
        receipt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        receipt.Arrange(new Rect(new Point((paperDiu - printableDiu) / 2, 0), receipt.DesiredSize));
        receipt.UpdateLayout();

        var ticket = queue.DefaultPrintTicket.Clone();
        ticket.PageMediaSize = new PageMediaSize(paperDiu, receipt.DesiredSize.Height + 20);
        ticket.CopyCount = 1;
        queue.CurrentJobSettings.Description = documentName;

        XpsDocumentWriter writer = PrintQueue.CreateXpsDocumentWriter(queue);
        writer.Write(receipt, ticket);
        return null;
    }
}
