using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Printing;
using LCWaikiki.POS.WpfClient.Core.Services;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>Termal yazıcı: ESC/POS kodlaması ve gerçek bir fişin yazıcı baytlarına çevrilmesi.</summary>
[Collection("Printer")]
public class PrinterTests
{
    [Fact]
    public void Raster_is_split_into_bands_with_correct_headers()
    {
        const int width = 576, height = 300, bpr = width / 8;
        var packed = new byte[bpr * height];
        packed[0] = 0x80;

        var raster = EscPos.Raster(packed, width, height);

        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 72, 0, 0, 1 }, raster[..8]);    // 72 bayt x 256 satır
        var second = 8 + bpr * 256;
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 72, 0, 44, 0 }, raster[second..(second + 8)]);
        Assert.Equal(8 * 2 + packed.Length, raster.Length);
        Assert.Equal(0x80, raster[8]);
    }

    [Fact]
    public void Monochrome_threshold_keeps_gray_text_and_treats_transparent_as_paper()
    {
        // 4 piksel: siyah, DimGray (105), açık gri (220), saydam
        byte[] bgra = [0, 0, 0, 255, 105, 105, 105, 255, 220, 220, 220, 255, 0, 0, 0, 0];
        var packed = EscPos.ToMonochrome(bgra, 4, 1, 16);
        Assert.Equal(0b1100_0000, packed[0]);
    }

    [Fact]
    public void Job_starts_with_init_and_ends_with_feed_cut_and_optional_drawer_kick()
    {
        var job = EscPos.ReceiptJob(new byte[72], 576, 1, cut: true, feedLines: 4, kickDrawer: true);
        Assert.Equal(EscPos.Initialize, job[..2]);
        Assert.Equal([.. EscPos.Feed(4), .. EscPos.PartialCutWithFeed, .. EscPos.DrawerKick], job[^12..]);

        var noCut = EscPos.ReceiptJob(new byte[72], 576, 1, cut: false, feedLines: 2, kickDrawer: false);
        Assert.Equal(EscPos.Feed(2), noCut[^3..]);
    }

    [Theory]
    [InlineData(80, 576)]
    [InlineData(58, 384)]
    public void Real_receipt_is_printed_as_raster_at_paper_width(int paperMm, int dots)
    {
        var dir = Path.Combine(Path.GetTempPath(), "wpos-print-test-" + Guid.NewGuid().ToString("N")[..8]);
        var output = Path.Combine(dir, "fis.bin");
        try
        {
            RunSta(() =>
            {
                ReceiptService.OutputRoot = dir;
                var settings = new ClientSettings();
                settings.Printer.Enabled = true;
                settings.Printer.Name = "file:" + output;
                settings.Printer.PaperWidthMm = paperMm;
                var service = new ReceiptService(new ReceiptPrinter(settings));
                service.GenerateAndSaveReceiptAsync(SampleReceipt()).GetAwaiter().GetResult();
            });

            var bytes = File.ReadAllBytes(output);
            var (width, height, pixels) = DecodeRaster(bytes);
            Assert.Equal(dots, width);
            Assert.True(height > 400, $"fiş yüksekliği {height}");
            var black = pixels.Count(p => p);
            Assert.InRange(black * 100.0 / pixels.Length, 2, 40);   // metin var, sayfa simsiyah değil
            Assert.Equal(EscPos.PartialCutWithFeed, bytes[^4..]);
            Assert.Null(PrinterStatus.Instance.LastError);
            Assert.Single(Directory.GetFiles(dir, "*.pdf", SearchOption.AllDirectories));   // PDF de kaydedildi

            var keep = Environment.GetEnvironmentVariable("WPOS_PRINT_PREVIEW");
            if (!string.IsNullOrEmpty(keep)) SavePreview(width, height, pixels, Path.Combine(keep, $"fis-{paperMm}mm.png"));
        }
        finally
        {
            ReceiptService.OutputRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LCWaikiki", "WPOS", "Receipts");
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Missing_printer_reports_clear_error_without_throwing()
    {
        string? error = null;
        RunSta(() =>
        {
            var settings = new ClientSettings();
            settings.Printer.Enabled = true;
            settings.Printer.Name = "Boyle Bir Yazici Yok " + Guid.NewGuid().ToString("N")[..4];
            error = new ReceiptPrinter(settings).Print(new System.Windows.Controls.TextBlock { Text = "x" }, 100, "test");
        });
        Assert.Contains("bulunamadı", error);
        Assert.Contains("bulunamadı", PrinterStatus.Instance.LastError);
        PrinterStatus.Instance.Report(null);
    }

    private static ReceiptData SampleReceipt() => new(
        "001202611060042", "Magaza 1", "Test Kasiyer", new DateTime(2026, 11, 6, 14, 30, 0),
        [
            new ReceiptLineData("Erkek Slim Fit Pantolon", "8684001001", "Siyah", "M", 2, 299.99m, 0, 599.98m),
            new ReceiptLineData("Kadın Örgü Hırka", "8684023973583", "Ekru", "S", 1, 449.90m, 45m, 404.90m)
        ],
        1004.88m, 0, 1004.88m, [new ReceiptPaymentData("Nakit", 1100m)], 95.12m,
        HeaderLines: ["Müşteri: Şükrü Çağlar İnce"]);

    /// <summary>GS v 0 bantlarını tek görüntüde birleştirir (yazıcının basacağı nokta haritası).</summary>
    private static (int Width, int Height, bool[] Pixels) DecodeRaster(byte[] job)
    {
        var rows = new List<bool[]>();
        var width = 0;
        for (var i = 0; i + 8 <= job.Length; i++)
        {
            if (job[i] != 0x1D || job[i + 1] != 0x76 || job[i + 2] != 0x30) continue;
            var bpr = job[i + 4] | (job[i + 5] << 8);
            var h = job[i + 6] | (job[i + 7] << 8);
            width = bpr * 8;
            var p = i + 8;
            for (var y = 0; y < h; y++, p += bpr)
            {
                var row = new bool[width];
                for (var x = 0; x < width; x++) row[x] = (job[p + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                rows.Add(row);
            }
            i = p - 1;
        }
        return (width, rows.Count, rows.SelectMany(r => r).ToArray());
    }

    private static void SavePreview(int width, int height, bool[] pixels, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var gray = pixels.Select(p => p ? (byte)0 : (byte)255).ToArray();
        var bmp = BitmapSource.Create(width, height, 203, 203, PixelFormats.Gray8, null, gray, width);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Exception("STA işlemi başarısız", error);
    }
}
