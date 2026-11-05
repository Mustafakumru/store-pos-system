using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace LCWaikiki.POS.WpfClient.ViewModels;

/// <summary>Fis ekraninin hangi modda acilacagi (Ana Menu kisayollari).</summary>
public enum PosOpenMode { Invoice, TaxFree, Exceptional }

/// <summary>
/// Ana Menu: BELGE grubundaki Fatura / Tax Free / Istisnai Satis kisayollari ve
/// EFT-POS grubu.
///
/// EFT-POS NOTU: Uygulamada banka POS cihazi entegrasyonu yok; bu yuzden
/// cihaza "gun sonu" komutu GONDERILMEZ. Butonlar kasa kayitlarindaki kart
/// tahsilat/iade toplamlarini gosterir - kasiyer cihazdan aldigi gun sonu
/// slibiyle karsilastirir (mutabakat).
/// </summary>
public partial class MainMenuViewModel
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static string Money(decimal v) => v.ToString("N2", Tr) + " TL";

    /// <summary>Fis ekranini belirli bir modda acma istegi (LoginWindow baglar).</summary>
    public event Action<PosOpenMode>? OpenPosWithModeRequested;

    [RelayCommand]
    private async Task OpenInvoiceSaleAsync()
        => await RunWithOpenSessionAsync(() => OpenPosWithModeRequested?.Invoke(PosOpenMode.Invoice));

    [RelayCommand]
    private async Task OpenTaxFreeSaleAsync()
        => await RunWithOpenSessionAsync(() => OpenPosWithModeRequested?.Invoke(PosOpenMode.TaxFree));

    [RelayCommand]
    private async Task OpenExceptionalSaleAsync()
        => await RunWithOpenSessionAsync(() => OpenPosWithModeRequested?.Invoke(PosOpenMode.Exceptional));

    // ══════════════════════ EFT-POS ══════════════════════

    /// <summary>Pos Gunortasi: son Z'den bu yana kart tahsilat ozeti (ara mutabakat).</summary>
    [RelayCommand]
    private async Task PosMidDayAsync() => await ShowCardReconciliationAsync("POS GUNORTASI (Ara Mutabakat)");

    /// <summary>Pos Gunsonu: kart tahsilat ozeti - cihaz gun sonu slibi ile karsilastirilir.</summary>
    [RelayCommand]
    private async Task PosEndOfDayAsync() => await ShowCardReconciliationAsync("POS GUNSONU (Kart Mutabakati)");

    private async Task ShowCardReconciliationAsync(string title)
    {
        InfoMessage = null;
        try
        {
            IsBusy = true;
            var x = await _reportService.GetXReportAsync();
            var net = x.CardTotal - x.CardRefundTotal;
            ShowLines(title,
                $"{PeriodText(x.FromDate, x.ToDate)}  ·  Cihaz entegrasyonu yok: gun sonunu banka cihazindan alip " +
                "slipteki tutari asagidaki toplamla karsilastirin",
                new List<ReportLine>
                {
                    new("Kartli satis tahsilati", Money(x.CardTotal)),
                    new("Karta yapilan iadeler", "-" + Money(x.CardRefundTotal)),
                    new("Hediye karti satisi (kartla)", Money(x.GiftCardSoldCard)),
                    new("BEKLENEN CIHAZ TOPLAMI", Money(net + x.GiftCardSoldCard), IsTotal: true),
                    new("Islem sayisi", x.TransactionCount.ToString(Tr)),
                    new("Iade sayisi", x.RefundCount.ToString(Tr))
                });
        }
        catch (ApiException ex) when (ex.StatusCode == 403)
        {
            InfoMessage = "Bu rapor icin yetkiniz yok.";
        }
        catch (Exception ex)
        {
            InfoMessage = $"Kart mutabakati alinamadi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Pos Gunsonu Tekrar: son alinan Z raporundaki kart toplamlari.</summary>
    [RelayCommand]
    private async Task PosEndOfDayRepeatAsync()
    {
        InfoMessage = null;
        try
        {
            IsBusy = true;
            var x = await _reportService.GetXReportAsync();
            if (x.LastZReportNumber is not int z)
            {
                InfoMessage = "Henuz gun sonu (Z) alinmamis - tekrar edilecek gun sonu yok.";
                return;
            }
            var f = await _reportService.GetFinancialReportAsync(z, z, null, null);
            ShowLines("POS GUNSONU TEKRAR",
                $"Z #{z}  ·  {PeriodText(f.FromDate, f.ToDate)}",
                new List<ReportLine>
                {
                    new("Kartli satis tahsilati", Money(f.CardTotal)),
                    new("Karta yapilan iadeler", "-" + Money(f.CardRefundTotal)),
                    new("Hediye karti satisi (kartla)", Money(f.GiftCardSoldCard)),
                    new("CIHAZ TOPLAMI", Money(f.CardTotal - f.CardRefundTotal + f.GiftCardSoldCard), IsTotal: true),
                    new("Islem sayisi", f.TransactionCount.ToString(Tr))
                });
        }
        catch (Exception ex)
        {
            InfoMessage = $"Son gun sonu alinamadi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Eksik Gun Sonu: son Z'den bu yana gun sonu alinmamis gun var mi?</summary>
    [RelayCommand]
    private async Task MissingEndOfDayAsync()
    {
        InfoMessage = null;
        try
        {
            IsBusy = true;
            var x = await _reportService.GetXReportAsync();
            var openDays = (DateTime.Today - x.FromDate.Date).Days;
            if (x.TransactionCount == 0 && x.RefundCount == 0)
                InfoMessage = "Eksik gun sonu yok: son Z'den bu yana islem bulunmuyor.";
            else if (openDays >= 1)
                InfoMessage = $"DIKKAT: {x.FromDate:dd.MM.yyyy} tarihinden beri gun sonu (Z) alinmamis " +
                              $"({openDays} gun, {x.TransactionCount} islem). OKC > Z Raporu ile gun sonu alin.";
            else
                InfoMessage = "Eksik gun sonu yok: acik donem bugun basliyor.";
        }
        catch (Exception ex)
        {
            InfoMessage = $"Gun sonu kontrolu yapilamadi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Parametre Guncelle: appsettings.json'u (kurlar, hizli urunler, cekmece) yeniden yukler.</summary>
    [RelayCommand]
    private async Task ReloadParametersAsync()
    {
        InfoMessage = null;
        try
        {
            var current = App.Services.GetRequiredService<ClientSettings>();
            var fresh = ClientSettings.Load();
            current.Currency = fresh.Currency;
            current.QuickProducts = fresh.QuickProducts;
            current.CashDrawer = fresh.CashDrawer;
            current.Countries = fresh.Countries;
            await RefreshSessionAsync();
            InfoMessage = $"Parametreler guncellendi: USD {fresh.Currency.UsdRate:N2} / EUR {fresh.Currency.EurRate:N2}, " +
                          $"{fresh.QuickProducts.Count} hizli urun. (API adresi degisikligi icin uygulamayi yeniden baslatin.)";
        }
        catch (Exception ex)
        {
            InfoMessage = $"Parametreler okunamadi: {ex.Message}";
        }
    }
}
