using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace LCWaikiki.POS.WpfClient.Core.Services;

public record ReceiptLineData(
	string ProductName,
	string Barcode,
	string? Color,
	string? Size,
	int Quantity,
	decimal UnitPrice,
	decimal DiscountAmount,
	decimal LineTotal,
	decimal InstallmentSurcharge = 0
);

public record ReceiptPaymentData(
	string Label,
	decimal Amount
);

public record ReceiptData(
	string ReceiptNumber,
	string StoreLabel,
	string CashierName,
	DateTime SaleDate,
	List<ReceiptLineData> Items,
	decimal TotalAmount,
	decimal DiscountAmount,
	decimal FinalAmount,
	List<ReceiptPaymentData> Payments,
	decimal ChangeAmount,
	bool IsCopy = false,   // Son Belge Tekrari: "KOPYA" basilir, orijinal PDF'in uzerine yazilmaz
	string? DocumentTitle = null,   // mali fis olmayan belgeler (orn. hediye karti satisi) icin baslik
	List<string>? HeaderLines = null   // belge ust bilgisi: fatura alicisi, ulke, satis danismani
);

/// <summary>
/// Yazici olmadigi icin fisi WPF Visual olarak cizip PNG goruntuye cevirir,
/// sonra o goruntuyu PdfSharp ile bir PDF sayfasina gomup diske kaydeder.
/// Boylece PDF'teki metin/karakter kodlamasi hicbir zaman bozulmaz - cunku
/// PDF'e metin degil, zaten dogru render edilmis bir resim gomuluyor.
///
/// NOT: Gercek LC Waikiki fislerindeki magaza adresi/VKN/MersisNo bilgileri
/// magazaya ozgudur ve Store entity'sinde su an tutulmuyor (sadece Code,
/// Name var). Bu yuzden burada uydurma/sabit bir adres-VKN basilmiyor -
/// yanlis mali bilgi gostermemek icin bilerek atlandi. Store entity'sine
/// Address/Phone/TaxNumber eklenirse buraya kolayca tasinabilir.
/// </summary>
public class ReceiptService(LCWaikiki.POS.WpfClient.Core.Printing.ReceiptPrinter? printer = null)
{
	private readonly LCWaikiki.POS.WpfClient.Core.Printing.ReceiptPrinter? _printer = printer;

	private const double ReceiptWidthDiu = 340; // Border'in toplam genisligi (icerik alani = bu - 2*ContentMargin)
	private const double ContentMargin = 14;
	private const double RenderDpi = 150;

	private static readonly CultureInfo TrCulture = new("tr-TR");

	/// <summary>Fis PDF klasoru (testler gecici klasore yonlendirir).</summary>
	public static string OutputRoot { get; set; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LCWaikiki", "WPOS", "Receipts");

	public async Task<string> GenerateAndSaveReceiptAsync(ReceiptData data)
	{
		var visual = BuildReceiptVisual(data);
		var (pngBytes, widthPx, heightPx) = RenderToPng(visual);

		var filePath = GetOutputPath(data.IsCopy
			? $"{data.ReceiptNumber}_KOPYA_{DateTime.Now:HHmmss}"
			: data.ReceiptNumber);
		Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

		SaveAsPdf(pngBytes, widthPx, heightPx, filePath);

		// Termal yazici: PDF kaydedildikten SONRA (yazici hatasi belgeyi kaybettirmez).
		// Ayni gorsel yeniden kurulur; yazdirma olcekleme/yerlesimi PDF'i etkilemesin.
		if (_printer?.IsEnabled == true)
			_printer.Print(BuildReceiptVisual(data), ReceiptWidthDiu, Path.GetFileNameWithoutExtension(filePath));

		await Task.CompletedTask;
		return filePath;
	}

	// ── Fis gorunumunu WPF Visual olarak insa et ─────────────────
	private static FrameworkElement BuildReceiptVisual(ReceiptData data)
	{
		// Ic icerik: sabit genislik BURADA degil, sadece Border'da verilir.
		// StackPanel Width vermiyoruz - Border'in genisligini otomatik doldurur.
		// Boylece Width + Margin ust uste binip tasma (kirpilma) olmuyor.
		var panel = new StackPanel
		{
			Margin = new Thickness(ContentMargin)
		};

		AddCentered(panel, "LCW - WAIKIKI", 16, bold: true);
		AddCentered(panel, "www.lcwaikiki.com", 9);
		AddCentered(panel, data.StoreLabel, 10);
		AddDashedSeparator(panel);

		if (!string.IsNullOrEmpty(data.DocumentTitle))
		{
			AddCentered(panel, data.DocumentTitle, 11, bold: true);
			AddCentered(panel, "BILGI FISIDIR - MALI DEGERI YOKTUR", 9);
			AddDashedSeparator(panel);
		}

		if (data.IsCopy)
		{
			AddCentered(panel, "*** KOPYA - MALI DEGERI YOKTUR ***", 11, bold: true);
			AddCentered(panel, $"Tekrar basim: {DateTime.Now:dd.MM.yyyy HH:mm}", 9);
			AddDashedSeparator(panel);
		}

		AddSmallLabelValue(panel, "FIS NO", data.ReceiptNumber);
		AddSpacer(panel, 2);

		var dateRow = new Grid();
		dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		AddPlainToGrid(dateRow, 0, $"Tarih: {data.SaleDate:dd.MM.yyyy}", HorizontalAlignment.Left);
		AddPlainToGrid(dateRow, 1, $"Saat: {data.SaleDate:HH:mm}", HorizontalAlignment.Right);
		panel.Children.Add(dateRow);

		AddLeftText(panel, $"Kasiyer: {data.CashierName}", 10, gray: true);
		if (data.HeaderLines is { Count: > 0 })
		{
			foreach (var line in data.HeaderLines)
				AddLeftText(panel, line, 10, bold: true);
		}
		AddDashedSeparator(panel);

		foreach (var item in data.Items)
		{
			// Gercek fislerdeki gibi: barkod once (kucuk, gri), sonra kalin urun adi
			AddLeftText(panel, item.Barcode, 9, gray: true);
			AddLeftText(panel, item.ProductName, 11, bold: true);

			var detail = string.Join(" / ", new[] { item.Color, item.Size }
				.Where(s => !string.IsNullOrWhiteSpace(s)));
			if (!string.IsNullOrWhiteSpace(detail))
				AddLeftText(panel, detail, 9, gray: true);

			// Adet x birim fiyat (brut) -> indirim -> vade farki; satirlarin
			// toplami LineTotal'a esit olur.
			AddAmountRow(panel, $"{item.Quantity} x {FormatAmount(item.UnitPrice)}", item.Quantity * item.UnitPrice);

			if (item.DiscountAmount > 0)
				AddAmountRow(panel, "  Indirim", -item.DiscountAmount, gray: true);

			if (item.InstallmentSurcharge > 0)
				AddAmountRow(panel, "  Vade Farki", item.InstallmentSurcharge, gray: true);

			AddSpacer(panel, 6);
		}

		AddDashedSeparator(panel);
		AddAmountRow(panel, "Ara Toplam", data.TotalAmount);
		if (data.DiscountAmount > 0)
			AddAmountRow(panel, "Indirim", -data.DiscountAmount);
		AddDashedSeparator(panel);
		AddAmountRow(panel, "TOPLAM", data.FinalAmount, bold: true, fontSize: 14);
		AddDashedSeparator(panel);

		foreach (var payment in data.Payments)
			AddAmountRow(panel, payment.Label, payment.Amount);

		if (data.ChangeAmount > 0)
			AddAmountRow(panel, "Para Ustu", data.ChangeAmount);

		AddDashedSeparator(panel);
		AddCentered(panel, "Iyi gunler dileriz", 10);
		AddSpacer(panel, 4);

		var border = new Border
		{
			Width = ReceiptWidthDiu,
			Background = Brushes.White,
			Child = panel
		};
		return border;
	}

	// ── Yardimci UI olusturucular ─────────────────────────────────
	private static void AddCentered(StackPanel panel, string text, double fontSize, bool bold = false)
	{
		panel.Children.Add(new TextBlock
		{
			Text = text,
			FontFamily = new FontFamily("Consolas"),
			FontSize = fontSize,
			FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 1, 0, 1)
		});
	}

	private static void AddLeftText(StackPanel panel, string text, double fontSize, bool bold = false, bool gray = false)
	{
		panel.Children.Add(new TextBlock
		{
			Text = text,
			FontFamily = new FontFamily("Consolas"),
			FontSize = fontSize,
			FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
			Foreground = gray ? Brushes.DimGray : Brushes.Black,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 1, 0, 1)
		});
	}

	// Etiket ustte kucuk, deger altta - fis no gibi uzun degerler icin (tasma yapmaz)
	private static void AddSmallLabelValue(StackPanel panel, string label, string value)
	{
		panel.Children.Add(new TextBlock
		{
			Text = label,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 9,
			Foreground = Brushes.DimGray,
			Margin = new Thickness(0, 1, 0, 0)
		});
		panel.Children.Add(new TextBlock
		{
			Text = value,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 10,
			FontWeight = FontWeights.Bold,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 0, 0, 1)
		});
	}

	private static void AddPlainToGrid(Grid grid, int column, string text, HorizontalAlignment align)
	{
		var block = new TextBlock
		{
			Text = text,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 10,
			Foreground = Brushes.DimGray,
			HorizontalAlignment = align
		};
		Grid.SetColumn(block, column);
		grid.Children.Add(block);
	}

	// Sol: etiket, sag: * onekli tutar (gercek fis formati - "TL" yazilmiyor)
	private static void AddAmountRow(StackPanel panel, string label, decimal amount, bool bold = false, double fontSize = 11, bool gray = false)
	{
		var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		var foreground = gray ? Brushes.DimGray : Brushes.Black;

		var keyBlock = new TextBlock
		{
			Text = label,
			FontFamily = new FontFamily("Consolas"),
			FontSize = fontSize,
			FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
			Foreground = foreground,
			TextWrapping = TextWrapping.Wrap
		};
		Grid.SetColumn(keyBlock, 0);

		var valueBlock = new TextBlock
		{
			Text = FormatAmount(amount),
			FontFamily = new FontFamily("Consolas"),
			FontSize = fontSize,
			FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
			Foreground = foreground,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(8, 0, 0, 0)
		};
		Grid.SetColumn(valueBlock, 1);

		grid.Children.Add(keyBlock);
		grid.Children.Add(valueBlock);
		panel.Children.Add(grid);
	}

	// Gercek fislerdeki gibi kesikli ayrac cizgisi
	private static void AddDashedSeparator(StackPanel panel)
	{
		panel.Children.Add(new TextBlock
		{
			Text = new string('-', 46),
			FontFamily = new FontFamily("Consolas"),
			FontSize = 10,
			Foreground = Brushes.Black,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 3, 0, 3)
		});
	}

	private static void AddSpacer(StackPanel panel, double height)
		=> panel.Children.Add(new Border { Height = height });

	// Gercek ÖKC fis formati: "*" onekli, "TL" yazmaz
	private static string FormatAmount(decimal value)
		=> (value < 0 ? "-*" : "*") + Math.Abs(value).ToString("N2", TrCulture);

	// ── WPF Visual -> PNG byte[] ──────────────────────────────────
	private static (byte[] png, double widthPx, double heightPx) RenderToPng(FrameworkElement element)
	{
		element.Measure(new Size(ReceiptWidthDiu, double.PositiveInfinity));
		element.Arrange(new Rect(0, 0, ReceiptWidthDiu, element.DesiredSize.Height));
		element.UpdateLayout();

		var heightDiu = Math.Ceiling(element.DesiredSize.Height);
		var dpiScale = RenderDpi / 96.0;

		var pixelWidth = (int)Math.Ceiling(ReceiptWidthDiu * dpiScale);
		var pixelHeight = (int)Math.Ceiling(heightDiu * dpiScale);

		var rtb = new RenderTargetBitmap(
			pixelWidth, pixelHeight, RenderDpi, RenderDpi, PixelFormats.Pbgra32);
		rtb.Render(element);

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(rtb));

		using var ms = new MemoryStream();
		encoder.Save(ms);

		return (ms.ToArray(), pixelWidth, pixelHeight);
	}

	// ── PNG byte[] -> tek sayfalik PDF ────────────────────────────
	private static void SaveAsPdf(byte[] pngBytes, double widthPx, double heightPx, string filePath)
	{
		var widthPt = widthPx / RenderDpi * 72.0;
		var heightPt = heightPx / RenderDpi * 72.0;

		using var doc = new PdfDocument();
		var page = doc.AddPage();
		page.Width = XUnit.FromPoint(widthPt);
		page.Height = XUnit.FromPoint(heightPt);

		using var gfx = XGraphics.FromPdfPage(page);
		using var imageStream = new MemoryStream(pngBytes);
		var image = XImage.FromStream(imageStream);
		gfx.DrawImage(image, 0, 0, widthPt, heightPt);

		doc.Save(filePath);
	}

	// ── Kayit yolu: C:\ProgramData\LCWaikiki\WPOS\Receipts\{yyyy-MM}\{FisNo}.pdf ──
	private static string GetOutputPath(string receiptNumber)
	{
		var baseDir = Path.Combine(OutputRoot, DateTime.Now.ToString("yyyy-MM"));

		return Path.Combine(baseDir, $"{receiptNumber}.pdf");
	}
}
