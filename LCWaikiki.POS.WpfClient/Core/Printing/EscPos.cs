using System.IO;
namespace LCWaikiki.POS.WpfClient.Core.Printing;

/// <summary>
/// ESC/POS komutları (Epson TM, Xprinter, Bixolon, Sewoo ve uyumlu termal yazıcılar).
/// Fiş METİN olarak değil RASTER RESİM olarak gönderilir: yazıcının kod sayfası
/// ayarından bağımsız olarak Türkçe karakterler (ğ, ş, İ...) doğru basılır ve
/// ekrandaki/PDF'teki fişle birebir aynı görünür.
/// </summary>
public static class EscPos
{
    public static readonly byte[] Initialize = [0x1B, 0x40];                 // ESC @
    public static readonly byte[] PartialCutWithFeed = [0x1D, 0x56, 0x42, 0x00]; // GS V 66 0
    public static byte[] Feed(int lines) => [0x1B, 0x64, (byte)Math.Clamp(lines, 0, 255)]; // ESC d n
    /// <summary>Kasa çekmecesi darbesi (pin 2): ESC p 0 25 250.</summary>
    public static readonly byte[] DrawerKick = [0x1B, 0x70, 0x00, 0x19, 0xFA];

    /// <summary>Bazı yazıcılar tek raster komutunda ~256 satırdan fazlasını kabul etmez.</summary>
    public const int MaxRowsPerBand = 256;

    /// <summary>
    /// 1 bit/piksel, satır başına (width+7)/8 bayt, en anlamlı bit soldaki nokta (1 = siyah)
    /// görüntüyü GS v 0 bantlarına böler.
    /// </summary>
    public static byte[] Raster(byte[] packed, int widthDots, int heightDots)
    {
        var bytesPerRow = (widthDots + 7) / 8;
        if (packed.Length != bytesPerRow * heightDots)
            throw new ArgumentException("Raster boyutu genişlik/yükseklikle uyuşmuyor.", nameof(packed));

        using var ms = new MemoryStream();
        for (var y = 0; y < heightDots; y += MaxRowsPerBand)
        {
            var rows = Math.Min(MaxRowsPerBand, heightDots - y);
            ms.Write([0x1D, 0x76, 0x30, 0x00,                                   // GS v 0 m=0 (normal)
                      (byte)(bytesPerRow & 0xFF), (byte)(bytesPerRow >> 8),     // xL xH (bayt)
                      (byte)(rows & 0xFF), (byte)(rows >> 8)]);                 // yL yH (nokta)
            ms.Write(packed, y * bytesPerRow, rows * bytesPerRow);
        }
        return ms.ToArray();
    }

    /// <summary>Tam fiş işi: başlat + raster + besle + (isteğe bağlı) kes + (isteğe bağlı) çekmece.</summary>
    public static byte[] ReceiptJob(byte[] packed, int widthDots, int heightDots, bool cut, int feedLines, bool kickDrawer)
    {
        using var ms = new MemoryStream();
        ms.Write(Initialize);
        ms.Write(Raster(packed, widthDots, heightDots));
        ms.Write(Feed(feedLines));
        if (cut) ms.Write(PartialCutWithFeed);
        if (kickDrawer) ms.Write(DrawerKick);
        return ms.ToArray();
    }

    /// <summary>
    /// 32 bit BGRA pikselleri 1 bit'e çevirir. Parlaklık eşiğin altındaysa siyah:
    /// varsayılan 180, fişteki gri (DimGray) yazıların da basılması için yüksek tutulur.
    /// </summary>
    public static byte[] ToMonochrome(byte[] bgra, int width, int height, int stride, int threshold = 180)
    {
        var bytesPerRow = (width + 7) / 8;
        var packed = new byte[bytesPerRow * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * 4;
                var a = bgra[i + 3];
                // Saydam = beyaz kağıt; ön çarpımlı renkte beyaz zemin üzerinde parlaklık
                var lum = (bgra[i + 2] * 299 + bgra[i + 1] * 587 + bgra[i] * 114) / 1000 + (255 - a);
                if (lum < threshold)
                    packed[y * bytesPerRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }
        return packed;
    }

    /// <summary>Kağıt genişliğine göre yazdırılabilir nokta sayısı (203 dpi): 80 mm → 576, 58 mm → 384.</summary>
    public static int DotsForPaper(int paperWidthMm) => paperWidthMm <= 58 ? 384 : 576;
}
