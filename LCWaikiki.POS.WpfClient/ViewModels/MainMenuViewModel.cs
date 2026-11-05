using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Globalization;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class MainMenuViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CashSessionService _cashSessionService;

    public string CashierName =>
        _authService.CurrentUser?.FullName ?? "Kasiyer";
    public string StoreName =>
       _authService.CurrentUser?.StoreId.ToString() ?? "";

    public event Action? LogoutRequested;
    public event Action? OpenPosRequested;
    public event Action? OpenExchangeRequested;

    private readonly ReportService _reportService;
    private readonly PosService _posService;
    private readonly ReceiptService _receiptService;

    private readonly GiftCardService _giftCardService;

    public MainMenuViewModel(AuthService authService, CashSessionService cashSessionService,
                             ReportService reportService, PosService posService,
                             ReceiptService receiptService, GiftCardService giftCardService,
                             ProductLookupViewModel lookupViewModel)
    {
        LookupVM = lookupViewModel;
        // Ana Menu'den "Sepete Ekle": Fis ekrani acilir ve urun eklenir
        LookupVM.AddToCartRequested += barcode => AddToPosRequested?.Invoke(barcode);
        _authService = authService;
        _cashSessionService = cashSessionService;
        _reportService = reportService;
        _posService = posService;
        _receiptService = receiptService;
        _giftCardService = giftCardService;
        GcLines.CollectionChanged += (_, _) => RecalcGiftCardSale();
    }

    // ══════════════════════ HEDIYE KARTI SATISI (EMANET) ══════════════════════
    // Musteri oder, kart(lar)a bakiye yuklenir. Ciroya girmez (emanet); nakit
    // kasa sayimina girer. Tek islemde birden cok kart (Coklu H.Kart Yukleme).

    public record GiftCardLine(string CardNumber, decimal Amount);

    public System.Collections.ObjectModel.ObservableCollection<GiftCardLine> GcLines { get; } = new();

    [ObservableProperty] private bool _isGiftCardSaleOpen;
    [ObservableProperty] private string _gcCardInput = string.Empty;
    [ObservableProperty] private string _gcAmountInput = string.Empty;
    [ObservableProperty] private string _gcCashInput = string.Empty;
    [ObservableProperty] private bool _gcPayByCard;
    [ObservableProperty] private decimal _gcTotal;
    [ObservableProperty] private decimal _gcChange;
    [ObservableProperty] private string? _gcError;

    partial void OnGcCashInputChanged(string value) => RecalcGiftCardSale();
    partial void OnGcPayByCardChanged(bool value) => RecalcGiftCardSale();

    private void RecalcGiftCardSale()
    {
        GcTotal = GcLines.Sum(l => l.Amount);
        GcChange = !GcPayByCard && TryParseMoney(GcCashInput, out var cash) && cash > GcTotal
            ? cash - GcTotal : 0;
    }

    private static bool TryParseMoney(string input, out decimal value)
        => decimal.TryParse((input ?? string.Empty).Replace(",", "."), NumberStyles.Number,
               CultureInfo.InvariantCulture, out value);

    [RelayCommand]
    private async Task OpenGiftCardSaleAsync()
    {
        InfoMessage = null;
        await RefreshSessionAsync();
        if (!IsSessionOpen)
        {
            // Kasa kapali: once Avans, sonra bu ekran
            _afterOpen = () => OpenGiftCardSalePanel();
            ShowCashPanel(closing: false);
            CashPanelError = "Kasa acik degil. Hediye karti satisi icin once avans girip kasayi acin.";
            return;
        }
        OpenGiftCardSalePanel();
    }

    // Hediye karti satisi tekrar gonderim anahtari: panel her acildiginda yeni,
    // ayni panelde "Tamamla"ya tekrar basilirsa (ag hatasi) ayni anahtar gider.
    private string _gcIdempotencyKey = Guid.NewGuid().ToString("N");

    private void OpenGiftCardSalePanel()
    {
        _gcIdempotencyKey = Guid.NewGuid().ToString("N");
        GcLines.Clear();
        GcCardInput = GcAmountInput = GcCashInput = string.Empty;
        GcPayByCard = false;
        GcError = null;
        IsGiftCardSaleOpen = true;
    }

    [RelayCommand]
    private void AddGiftCardLine()
    {
        GcError = null;
        var card = GcCardInput.Trim();
        if (card.Length < 4) { GcError = "Gecerli bir kart numarasi okutun/girin."; return; }
        if (GcLines.Any(l => l.CardNumber == card)) { GcError = "Bu kart zaten listede."; return; }
        if (!TryParseMoney(GcAmountInput, out var amount) || amount < 1 || amount > GiftCardService.MaxPerCard)
        {
            GcError = $"Yukleme tutari 1 - {GiftCardService.MaxPerCard:N0} TL olmalidir.";
            return;
        }
        GcLines.Add(new GiftCardLine(card, Math.Round(amount, 2)));
        GcCardInput = GcAmountInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveGiftCardLine(GiftCardLine line) => GcLines.Remove(line);

    [RelayCommand]
    private void SetGcPayment(string method) => GcPayByCard = method == "Card";

    [RelayCommand]
    private void CancelGiftCardSale() => IsGiftCardSaleOpen = false;

    [RelayCommand]
    private async Task CompleteGiftCardSaleAsync()
    {
        GcError = null;
        if (GcLines.Count == 0) { GcError = "En az bir kart ekleyin."; return; }

        decimal paid;
        if (GcPayByCard) paid = GcTotal;   // karttan tam tutar cekilir (para ustu yok)
        else if (!TryParseMoney(GcCashInput, out paid) || paid < GcTotal)
        {
            GcError = $"Alinan nakit en az {GcTotal:N2} TL olmalidir.";
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _giftCardService.SellAsync(
                GcLines.Select(l => new GiftCardSaleLineRequest(l.CardNumber, l.Amount)).ToList(),
                new() { new SalePaymentRequest(GcPayByCard ? (int)PaymentType.Card : (int)PaymentType.Cash, paid) },
                _gcIdempotencyKey);

            // Bilgi belgesi (mali fis degil)
            string? pdf = null;
            try
            {
                pdf = await _receiptService.GenerateAndSaveReceiptAsync(new ReceiptData(
                    ReceiptNumber: result.DocumentNumber,
                    StoreLabel: $"Magaza {_authService.CurrentUser?.StoreId}",
                    CashierName: CashierName,
                    SaleDate: result.SoldAt,
                    Items: result.Cards.Select(c => new ReceiptLineData(
                        "Hediye Karti Yukleme", MaskCard(c.CardNumber), null, $"Yeni bakiye {c.NewBalance:N2} TL",
                        1, c.LoadedAmount, 0, c.LoadedAmount, 0)).ToList(),
                    TotalAmount: result.TotalAmount,
                    DiscountAmount: 0,
                    FinalAmount: result.TotalAmount,
                    Payments: new() { new ReceiptPaymentData(GcPayByCard ? "Kart" : "Nakit", paid) },
                    ChangeAmount: result.ChangeAmount,
                    DocumentTitle: "HEDIYE KARTI SATIS BELGESI"));
            }
            catch { /* belge basilamasa da satis kaydedildi */ }

            IsGiftCardSaleOpen = false;
            InfoMessage = $"Hediye karti satisi tamam: {result.DocumentNumber}  ·  {result.Cards.Count} kart  ·  " +
                          $"{result.TotalAmount:N2} TL" +
                          (result.ChangeAmount > 0 ? $"  ·  PARA USTU {result.ChangeAmount:N2} TL" : "") +
                          (pdf is null ? "  (belge PDF olusturulamadi)" : "");
        }
        catch (ApiException ex)
        {
            GcError = ex.Message;
        }
        catch (Exception ex)
        {
            GcError = $"Baglanti hatasi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private static string MaskCard(string n) => n.Length <= 4 ? n : new string('*', n.Length - 4) + n[^4..];

    /// <summary>Ortak panel hangi islem icin acik.</summary>
    private enum PanelMode { Cash, ZReport, PriceCheck, GiftCard, Financial, Reprint }
    private PanelMode _panelMode = PanelMode.Cash;
    private LastSaleDto? _reprintTarget;

    [ObservableProperty] private string _amountLabel = "Avans tutari (TL)";

    /// <summary>Ortak paneli giris alanli/onayli bir sorgu islemi icin acar.</summary>
    private void ShowToolPanel(PanelMode mode, string title, string hint, string? inputLabel,
                               bool needsApproval, string confirmText)
    {
        InfoMessage = null;
        _panelMode = mode;
        _afterOpen = null;
        IsZMode = false;
        IsClosingMode = false;
        CashPanelTitle = title;
        CashPanelHint = hint;
        ShowAmountField = inputLabel is not null;
        AmountLabel = inputLabel ?? string.Empty;
        AmountInput = string.Empty;
        ShowApprovalFields = needsApproval;
        ConfirmButtonText = confirmText;
        ManagerPersonelId = string.Empty;
        ManagerPin = string.Empty;
        ClearPinRequested?.Invoke();
        CashPanelError = null;
        IsCashPanelOpen = true;
    }

    // ── Fiyat Gor ──
    [RelayCommand]
    private void OpenPriceCheck()
    {
        // Barkodsuz urunler de bulunabilsin: urun kodu -> renk -> beden paneli
        InfoMessage = null;
        LookupVM.Open(canAddToCart: true);
    }

    /// <summary>Urun Bul / Fiyat Gor paneli (urun kodu, barkod, ad).</summary>
    public ProductLookupViewModel LookupVM { get; }

    /// <summary>Panelden "Sepete Ekle": Fis ekranina bu barkodla urun eklenir.</summary>
    public event Action<string>? AddToPosRequested;

    // ── Hediye karti bakiye sorgu ──
    [RelayCommand]
    private void OpenGiftCardQuery() => ShowToolPanel(PanelMode.GiftCard, "HEDIYE KARTI SORGU",
        "Kart numarasini girin. (Bakiye yuklemek icin 'H.Kart Satis'.)",
        "Kart numarasi", false, "Sorgula");

    // ── Mali rapor ──
    [RelayCommand]
    private void OpenFinancialReport() => ShowToolPanel(PanelMode.Financial, "MALI RAPOR",
        "Kayitli Z raporlarinin toplami. Z no araligi girin (orn. 1-5) veya bos birakin: son 30 gun.",
        "Z no araligi (bos = son 30 gun)", false, "Raporla");

    // ── Son belge tekrari ──
    [RelayCommand]
    private async Task ReprintLastAsync()
    {
        InfoMessage = null;
        try
        {
            IsBusy = true;
            _reprintTarget = await _posService.GetLastSaleAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            InfoMessage = "Tekrar basilacak tamamlanmis satisiniz yok.";
            return;
        }
        catch (Exception ex)
        {
            InfoMessage = $"Son belge alinamadi: {ex.Message}";
            return;
        }
        finally { IsBusy = false; }

        ShowToolPanel(PanelMode.Reprint, "SON BELGE TEKRARI",
            $"Fis {_reprintTarget.ReceiptNumber}  ·  {_reprintTarget.SaleDate:dd.MM.yyyy HH:mm}  ·  " +
            $"{_reprintTarget.FinalAmount:N2} TL\nFisin KOPYASI basilir (mali degeri yoktur). Mudur onayi gerekir.",
            null, true, "Kopya Bas");
    }

    // ── Kasa raporu (beklenen kasa) - sadece mudur ──
    [RelayCommand]
    private async Task CashReportAsync()
    {
        InfoMessage = null;
        if (!IsManagerUser)
        {
            InfoMessage = "Kasa raporu (beklenen tutar) sadece magaza mudurune aciktir - teslim kor sayimdir.";
            return;
        }
        try
        {
            IsBusy = true;
            var r = await _reportService.GetCashReportAsync();
            static string M(decimal v) => $"{v:N2} TL";
            ShowLines("KASA RAPORU", $"Kendi oturumunuz  ·  Acilis {r.OpenedAt:dd.MM.yyyy HH:mm}", new()
            {
                new("Avans", M(r.OpeningFloat)),
                new("Nakit tahsilat", M(r.CashSalesTotal)),
                new("Nakit iade", "-" + M(r.CashRefundTotal)),
                new("H.kart satisi nakit (emanet)", M(r.GiftCardSoldCash)),
                new("Beklenen kasa", M(r.ExpectedCashAmount), IsTotal: true),
                new("Islem sayisi", r.TransactionCount.ToString())
            });
        }
        catch (Exception ex)
        {
            InfoMessage = $"Kasa raporu alinamadi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task<bool> RunToolPanelAsync()
    {
        static string M(decimal v) => $"{v:N2} TL";
        var input = AmountInput.Trim();

        switch (_panelMode)
        {
            case PanelMode.PriceCheck:
                if (input.Length == 0) { CashPanelError = "Barkod girin."; return false; }
                var p = await _posService.PriceCheckAsync(input);
                ShowLines("FIYAT", p.Name, new()
                {
                    new("Barkod", p.Barcode),
                    new("Fiyat", M(p.CurrentPrice), IsTotal: true),
                    new("Magaza stogu", p.StockQuantity?.ToString() ?? "-")
                });
                return true;

            case PanelMode.GiftCard:
                if (input.Length == 0) { CashPanelError = "Kart numarasi girin."; return false; }
                var g = await _posService.GetGiftCardAsync(input);
                ShowLines("HEDIYE KARTI", g.CardNumber, new()
                {
                    new("Bakiye", M(g.Balance), IsTotal: true),
                    new("Son kullanma", g.ExpiryDate.ToString("dd.MM.yyyy")),
                    new("Durum", !g.IsActive ? "PASIF" : g.ExpiryDate < DateTime.Now ? "SURESI DOLMUS" : "Aktif")
                });
                return true;

            case PanelMode.Financial:
                FinancialReportDto f;
                if (input.Length == 0)
                    f = await _reportService.GetFinancialReportAsync(null, null, DateTime.Now.Date.AddDays(-30), DateTime.Now);
                else
                {
                    var parts = input.Split('-', StringSplitOptions.TrimEntries);
                    if (parts.Length != 2 || !int.TryParse(parts[0], out var fz) || !int.TryParse(parts[1], out var tz) || fz > tz)
                    {
                        CashPanelError = "Z araligini '1-5' biciminde girin.";
                        return false;
                    }
                    f = await _reportService.GetFinancialReportAsync(fz, tz, null, null);
                }
                ShowLines("MALI RAPOR",
                    $"Z #{f.FromZNumber} - #{f.ToZNumber} ({f.ZReportCount} adet)  ·  {f.FromDate:dd.MM.yyyy} → {f.ToDate:dd.MM.yyyy}",
                    new()
                    {
                        new("Islem sayisi", f.TransactionCount.ToString()),
                        new("Iade islemi", f.RefundCount.ToString()),
                        new("Satis toplami", M(f.TotalSalesAmount)),
                        new("Iade toplami", "-" + M(f.TotalRefundsAmount)),
                        new("NET", M(f.NetAmount), IsTotal: true),
                        new("Nakit", M(f.CashTotal)),
                        new("Nakit iade", "-" + M(f.CashRefundTotal)),
                        new("Kart", M(f.CardTotal)),
                        new("Karta iade", "-" + M(f.CardRefundTotal)),
                        new("Hediye karti ile odeme", M(f.GiftCardTotal)),
                        new("H.kart satisi (emanet)", M(f.GiftCardSoldTotal))
                    });
                return true;

            case PanelMode.Reprint:
                if (_reprintTarget is null) return false;
                if (string.IsNullOrWhiteSpace(ManagerPersonelId) || string.IsNullOrWhiteSpace(ManagerPin))
                {
                    CashPanelError = "Magaza muduru kodu ve PIN gereklidir.";
                    return false;
                }
                var token = (await _authService.GetApprovalTokenAsync(
                    ManagerPersonelId.Trim(), ManagerPin, "reprint")).ApprovalToken;
                var sale = await _posService.ReprintAsync(_reprintTarget.Id, token);
                var lineSum = sale.Items.Sum(i => i.LineTotal);
                var path = await _receiptService.GenerateAndSaveReceiptAsync(new ReceiptData(
                    ReceiptNumber: sale.ReceiptNumber,
                    StoreLabel: sale.StoreName,
                    CashierName: CashierName,
                    SaleDate: sale.SaleDate,
                    Items: sale.Items.Select(i => new ReceiptLineData(
                        i.ProductName, i.Barcode, i.Color, i.Size, i.Quantity,
                        i.PriceAtSale, i.DiscountAmount, i.LineTotal, 0)).ToList(),
                    TotalAmount: lineSum,
                    DiscountAmount: lineSum - sale.FinalAmount,
                    FinalAmount: sale.FinalAmount,
                    Payments: sale.Payments.Select(pm => new ReceiptPaymentData(PaymentLabel(pm.PaymentType), pm.Amount)).ToList(),
                    ChangeAmount: 0,
                    IsCopy: true));
                InfoMessage = $"Fis kopyasi kaydedildi: {path}";
                return true;
        }
        return false;
    }

    private static string PaymentLabel(string type) => type switch
    {
        "Cash" => "Nakit",
        "Card" => "Kart",
        "GiftCard" => "Hediye Karti",
        "ExchangeCredit" => "Iade Kredisi",
        _ => type
    };

    private void ShowLines(string title, string subtitle, List<ReportLine> lines)
    {
        ReportTitle = title;
        ReportSubtitle = subtitle;
        ReportLines.Clear();
        foreach (var l in lines) ReportLines.Add(l);
        IsReportOpen = true;
    }

    /// <summary>Giris yapan magaza muduru/Admin mi (Z icin ayrica onay istenmez).</summary>
    private bool IsManagerUser =>
        _authService.CurrentUser?.Roles?.Any(r => r is "StoreManager" or "Admin") == true;

    // ── Panel modu: Avans / Teslim / Z (ayni panel, farkli alanlar) ──
    [ObservableProperty] private bool _isZMode;
    [ObservableProperty] private bool _showAmountField = true;
    [ObservableProperty] private bool _showApprovalFields = true;
    [ObservableProperty] private string _confirmButtonText = "Kasayi Ac";

    // ══════════════════════ KASA OTURUMU (AVANS / TESLIM) ══════════════════════
    // Backend satis/iade/degisimi acik kasa oturumu olmadan reddeder. Fis ve
    // Iade-Degisim ekranlari acilmadan once kasa durumu kontrol edilir.

    [ObservableProperty] private bool _isSessionOpen;
    [ObservableProperty] private string _sessionStatusText = "Kasa durumu kontrol ediliyor...";
    [ObservableProperty] private string? _infoMessage;

    [ObservableProperty] private bool _isCashPanelOpen;
    [ObservableProperty] private bool _isClosingMode;          // false = Avans, true = Teslim
    [ObservableProperty] private string _cashPanelTitle = string.Empty;
    [ObservableProperty] private string _cashPanelHint = string.Empty;
    [ObservableProperty] private string _amountInput = string.Empty;
    [ObservableProperty] private string _managerPersonelId = string.Empty;
    [ObservableProperty] private string? _cashPanelError;
    [ObservableProperty] private bool _isBusy;

    /// <summary>PasswordBox binding desteklemedigi icin code-behind'dan set edilir.</summary>
    public string ManagerPin { get; set; } = string.Empty;
    public event Action? ClearPinRequested;

    /// <summary>Kasa acildiktan sonra devam edilecek islem (Fis / Iade-Degisim).</summary>
    private Action? _afterOpen;

    public async Task RefreshSessionAsync()
    {
        try
        {
            var s = await _cashSessionService.GetCurrentAsync();
            IsSessionOpen = s.IsOpen;
            SessionStatusText = s.IsOpen
                ? $"Kasa ACIK  ·  Avans {s.OpeningFloat:N2} TL  ·  {s.OpenedAt:HH:mm}"
                : "Kasa KAPALI  ·  Satis icin once Avans girin";
        }
        catch (Exception ex)
        {
            SessionStatusText = $"Kasa durumu alinamadi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenPosAsync() => await RunWithOpenSessionAsync(() => OpenPosRequested?.Invoke());

    [RelayCommand]
    private async Task OpenExchangeAsync() => await RunWithOpenSessionAsync(() => OpenExchangeRequested?.Invoke());

    private async Task RunWithOpenSessionAsync(Action action)
    {
        await RefreshSessionAsync();
        if (IsSessionOpen)
        {
            action();
            return;
        }

        // Kasa kapali: once Avans paneli, onaylaninca istenen ekran acilir
        _afterOpen = action;
        ShowCashPanel(closing: false);
        CashPanelError = "Kasa acik degil. Satis/iade icin once avans girip kasayi acin.";
    }

    [RelayCommand]
    private async Task OpenCashSessionAsync(string action)
    {
        InfoMessage = null;
        await RefreshSessionAsync();
        var closing = action == "close";

        if (closing && !IsSessionOpen)
        {
            InfoMessage = "Acik kasa oturumunuz yok - teslim edilecek bir sey bulunmuyor.";
            return;
        }
        if (!closing && IsSessionOpen)
        {
            InfoMessage = "Kasaniz zaten acik. Yeni avans icin once Teslim ile kapatin.";
            return;
        }

        _afterOpen = null;
        ShowCashPanel(closing);
    }

    private void ShowCashPanel(bool closing)
    {
        _panelMode = PanelMode.Cash;
        AmountLabel = closing ? "Sayilan nakit toplami (TL)" : "Avans tutari (TL)";
        IsZMode = false;
        ShowAmountField = true;
        ShowApprovalFields = true;
        ConfirmButtonText = closing ? "Teslim Et" : "Kasayi Ac";
        IsClosingMode = closing;
        CashPanelTitle = closing ? "TESLIM (Kasa Kapanis)" : "AVANS (Kasa Acilis)";
        CashPanelHint = closing
            ? "Kasadaki nakdi sayin ve sayilan toplami girin. Beklenen tutar gosterilmez (kor sayim); fark magaza panelinde gorulur."
            : "Kasaya konulan acilis nakdini (bozuk para dahil) girin.";
        AmountInput = string.Empty;
        ManagerPersonelId = string.Empty;
        ManagerPin = string.Empty;
        ClearPinRequested?.Invoke();
        CashPanelError = null;
        IsCashPanelOpen = true;
    }

    [RelayCommand]
    private void CancelCashPanel()
    {
        IsCashPanelOpen = false;
        _afterOpen = null;
        ManagerPin = string.Empty;
        ClearPinRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ConfirmCashPanelAsync()
    {
        CashPanelError = null;

        if (IsZMode)
        {
            await ConfirmZReportAsync();
            return;
        }

        if (_panelMode is not PanelMode.Cash)
        {
            try
            {
                IsBusy = true;
                if (await RunToolPanelAsync())
                {
                    IsCashPanelOpen = false;
                    ManagerPin = string.Empty;
                    ClearPinRequested?.Invoke();
                }
            }
            catch (ApiException ex)
            {
                CashPanelError = ex.StatusCode == 404 ? "Kayit bulunamadi." : ex.Message;
                ManagerPin = string.Empty;
                ClearPinRequested?.Invoke();
            }
            catch (Exception ex)
            {
                CashPanelError = $"Hata: {ex.Message}";
            }
            finally { IsBusy = false; }
            return;
        }

        if (!decimal.TryParse(AmountInput.Replace(",", "."), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            CashPanelError = "Gecerli bir tutar girin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ManagerPersonelId) || string.IsNullOrWhiteSpace(ManagerPin))
        {
            CashPanelError = "Magaza muduru kodu ve PIN gereklidir.";
            return;
        }

        try
        {
            IsBusy = true;
            var approval = await _authService.GetApprovalTokenAsync(
                ManagerPersonelId.Trim(), ManagerPin,
                IsClosingMode ? CashSessionService.CloseApprovalAction : CashSessionService.OpenApprovalAction);

            if (IsClosingMode)
            {
                var closed = await _cashSessionService.CloseAsync(amount, approval.ApprovalToken);
                InfoMessage = $"Teslim kaydedildi: {closed.ClosingAmount:N2} TL. Kasa kapatildi.";
            }
            else
            {
                var opened = await _cashSessionService.OpenAsync(amount, approval.ApprovalToken);
                InfoMessage = $"Kasa acildi. Avans: {opened.OpeningFloat:N2} TL";
            }

            IsCashPanelOpen = false;
            ManagerPin = string.Empty;
            ClearPinRequested?.Invoke();
            await RefreshSessionAsync();

            var next = _afterOpen;
            _afterOpen = null;
            if (!IsClosingMode && IsSessionOpen) next?.Invoke();
        }
        catch (ApiException ex)
        {
            CashPanelError = ex.Message;
            ManagerPin = string.Empty;
            ClearPinRequested?.Invoke();
        }
        catch (Exception ex)
        {
            CashPanelError = $"Baglanti hatasi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void DismissInfo() => InfoMessage = null;

    // ══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    // ══════════════════════════ X / Z RAPORU ══════════════════════════

    public record ReportLine(string Label, string Value, bool IsTotal = false);

    [ObservableProperty] private bool _isReportOpen;
    [ObservableProperty] private string _reportTitle = string.Empty;
    [ObservableProperty] private string _reportSubtitle = string.Empty;
    public System.Collections.ObjectModel.ObservableCollection<ReportLine> ReportLines { get; } = new();

    [RelayCommand]
    private void CloseReport() => IsReportOpen = false;

    /// <summary>X raporu: son Z'den bu yana ara toplam (sayac sifirlanmaz).</summary>
    [RelayCommand]
    private async Task XReportAsync()
    {
        InfoMessage = null;
        try
        {
            IsBusy = true;
            var x = await _reportService.GetXReportAsync();
            if (x.CashDetailsHidden)
            {
                // Kasiyer: nakit/ciro gizli (kör sayım). Sıfırları rapor gibi göstermeyiz.
                InfoMessage = "X raporu (ciro ve nakit) sadece Magaza Muduru tarafindan goruntulenebilir. " +
                              "Kart mutabakati icin 'POS Gunortasi / Gunsonu' kullanin.";
                return;
            }
            ShowReport(
                "X RAPORU (Ara Rapor)",
                $"{PeriodText(x.FromDate, x.ToDate)}" +
                (x.LastZReportNumber is int z ? $"  ·  Son Z: #{z}" : "  ·  Henuz Z alinmamis"),
                x.TransactionCount, x.RefundCount, x.TotalSalesAmount, x.TotalRefundsAmount, x.NetAmount,
                x.CashTotal, x.CashRefundTotal, x.CardTotal, x.CardRefundTotal, x.GiftCardTotal,
                x.GiftCardSoldTotal, x.GiftCardSoldCash, x.GiftCardSoldCard);
        }
        catch (Exception ex)
        {
            InfoMessage = $"X raporu alinamadi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Z raporu: once onay paneli (geri alinamaz uyarisi + mudur onayi).</summary>
    [RelayCommand]
    private async Task ZReportAsync()
    {
        InfoMessage = null;
        await RefreshSessionAsync();

        _panelMode = PanelMode.ZReport;
        IsZMode = true;
        IsClosingMode = false;
        ShowAmountField = false;
        ShowApprovalFields = !IsManagerUser;
        ConfirmButtonText = "Z Raporu Al";
        CashPanelTitle = "Z RAPORU (Gun Sonu)";
        CashPanelHint = "Z raporu donemi kapatir ve GERI ALINAMAZ. Magazadaki tum kasalar 'Teslim' ile " +
                        "kapatilmis olmalidir." + (IsManagerUser ? "" : " Magaza muduru onayi gerekir.");
        ManagerPersonelId = string.Empty;
        ManagerPin = string.Empty;
        ClearPinRequested?.Invoke();
        CashPanelError = IsSessionOpen
            ? "Kendi kasaniz hala acik. Once 'Teslim' ile kapatin."
            : null;
        _afterOpen = null;
        IsCashPanelOpen = true;
    }

    private async Task ConfirmZReportAsync()
    {
        if (!IsManagerUser && (string.IsNullOrWhiteSpace(ManagerPersonelId) || string.IsNullOrWhiteSpace(ManagerPin)))
        {
            CashPanelError = "Magaza muduru kodu ve PIN gereklidir.";
            return;
        }

        try
        {
            IsBusy = true;
            string? token = null;
            if (!IsManagerUser)
                token = (await _authService.GetApprovalTokenAsync(
                    ManagerPersonelId.Trim(), ManagerPin, ReportService.ZApprovalAction)).ApprovalToken;

            var z = await _reportService.CreateZReportAsync(token);

            IsCashPanelOpen = false;
            ManagerPin = string.Empty;
            ClearPinRequested?.Invoke();

            ShowReport(
                $"Z RAPORU  #{z.ReportNumber}",
                PeriodText(z.FromDate, z.ToDate) + "  ·  Donem kapatildi",
                z.TransactionCount, z.RefundCount, z.TotalSalesAmount, z.TotalRefundsAmount, z.NetAmount,
                z.CashTotal, z.CashRefundTotal, z.CardTotal, z.CardRefundTotal, z.GiftCardTotal,
                z.GiftCardSoldTotal, z.GiftCardSoldCash, z.GiftCardSoldCard);
        }
        catch (ApiException ex)
        {
            CashPanelError = ex.Message;
            ManagerPin = string.Empty;
            ClearPinRequested?.Invoke();
        }
        catch (Exception ex)
        {
            CashPanelError = $"Baglanti hatasi: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void ShowReport(string title, string subtitle, int txCount, int refundCount,
        decimal sales, decimal refunds, decimal net,
        decimal cash, decimal cashRefund, decimal card, decimal cardRefund, decimal giftCard,
        decimal gcSold = 0, decimal gcSoldCash = 0, decimal gcSoldCard = 0)
    {
        static string M(decimal v) => $"{v:N2} TL";
        ReportTitle = title;
        ReportSubtitle = subtitle;
        ReportLines.Clear();
        ReportLines.Add(new("Islem sayisi", txCount.ToString()));
        ReportLines.Add(new("Iade islemi", refundCount.ToString()));
        ReportLines.Add(new("Satis toplami", M(sales)));
        ReportLines.Add(new("Iade toplami", "-" + M(refunds)));
        ReportLines.Add(new("NET", M(net), IsTotal: true));
        ReportLines.Add(new("Nakit tahsilat", M(cash)));
        ReportLines.Add(new("Nakit iade", "-" + M(cashRefund)));
        ReportLines.Add(new("H.kart satisi nakit (emanet)", M(gcSoldCash)));
        ReportLines.Add(new("Kasaya net nakit", M(cash - cashRefund + gcSoldCash), IsTotal: true));
        ReportLines.Add(new("Kart tahsilat", M(card)));
        ReportLines.Add(new("Karta iade", "-" + M(cardRefund)));
        ReportLines.Add(new("Hediye karti ile odeme", M(giftCard)));
        ReportLines.Add(new("H.kart satisi (emanet, ciro degil)", M(gcSold)));
        ReportLines.Add(new("  - karttan tahsil", M(gcSoldCard)));
        IsReportOpen = true;
    }

    private static string PeriodText(DateTime from, DateTime to)
        => from.Year <= 1
            ? $"Baslangic → {to:dd.MM.yyyy HH:mm}"
            : $"{from:dd.MM.yyyy HH:mm} → {to:dd.MM.yyyy HH:mm}";

    // ══════════════════════════ SIFRE (PIN) DEGISTIR ══════════════════════════

    [ObservableProperty] private bool _isChangePinOpen;
    [ObservableProperty] private string? _changePinError;
    public string OldPin { get; set; } = string.Empty;
    public string NewPin { get; set; } = string.Empty;
    public string NewPinRepeat { get; set; } = string.Empty;
    public event Action? ClearChangePinRequested;

    [RelayCommand]
    private void OpenChangePin()
    {
        InfoMessage = null;
        ChangePinError = null;
        OldPin = NewPin = NewPinRepeat = string.Empty;
        ClearChangePinRequested?.Invoke();
        IsChangePinOpen = true;
    }

    [RelayCommand]
    private void CancelChangePin()
    {
        IsChangePinOpen = false;
        OldPin = NewPin = NewPinRepeat = string.Empty;
        ClearChangePinRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ConfirmChangePinAsync()
    {
        ChangePinError = null;
        if (string.IsNullOrEmpty(OldPin) || string.IsNullOrEmpty(NewPin))
        {
            ChangePinError = "Mevcut ve yeni PIN'i girin.";
            return;
        }
        if (NewPin != NewPinRepeat)
        {
            ChangePinError = "Yeni PIN'ler eslesmiyor.";
            return;
        }

        try
        {
            IsBusy = true;
            await _authService.ChangePinAsync(OldPin, NewPin);
            IsChangePinOpen = false;
            InfoMessage = "PIN'iniz degistirildi.";
        }
        catch (ApiException ex)
        {
            ChangePinError = ex.Message;   // orn. "Mevcut PIN hatali", zayif PIN, hesap kilitli
        }
        catch (Exception ex)
        {
            ChangePinError = $"Baglanti hatasi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OldPin = NewPin = NewPinRepeat = string.Empty;
            ClearChangePinRequested?.Invoke();
        }
    }
}
