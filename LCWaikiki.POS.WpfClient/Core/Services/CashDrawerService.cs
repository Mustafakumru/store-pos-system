using LCWaikiki.POS.WpfClient.Core.Printing;

namespace LCWaikiki.POS.WpfClient.Core.Services;

/// <summary>
/// Kasa cekmecesini acar. Cekmeceler fis yazicisinin RJ11 portuna baglidir ve
/// yaziciya gonderilen ESC/POS "darbe" komutuyla (ESC p 0 25 250) acilir.
/// Yazici adi appsettings.json > CashDrawer:PrinterName'den okunur.
/// </summary>
public class CashDrawerService(ClientSettings settings)
{
    private readonly ClientSettings _settings = settings;

    /// <summary>Cekmecenin bagli oldugu yazici: ayri tanimli degilse fis yazicisi.</summary>
    private string? PrinterName => !string.IsNullOrWhiteSpace(_settings.CashDrawer.PrinterName)
        ? _settings.CashDrawer.PrinterName
        : _settings.Printer.Enabled ? _settings.Printer.Name : null;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PrinterName);

    /// <summary>Cekmeceyi acar; basarisizsa aciklayici bir hata mesaji doner (basariliysa null).</summary>
    public string? Open()
    {
        if (!IsConfigured)
            return "Kasa cekmecesi tanimli degil. appsettings.json > CashDrawer:PrinterName alanina " +
                   "cekmecenin bagli oldugu fis yazicisinin adini yazin.";

        var printer = PrinterName!;
        try
        {
            return RawPrinter.Send(printer, EscPos.DrawerKick, "WPOS Cekmece");
        }
        catch (Exception ex)
        {
            return $"Cekmece acilamadi: {ex.Message}";
        }
    }
}
