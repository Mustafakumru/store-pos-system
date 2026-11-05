using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Offline;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Collections.ObjectModel;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class PaymentLine : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private PaymentType _type;
    [ObservableProperty] private int? _installmentCount;
    [ObservableProperty] private string? _cardNumber;
}

public partial class PaymentViewModel : BaseViewModel
{
    private readonly PosService _posService;
    private readonly AuthService _authService;

    // ── Sepet ozeti (MainPosViewModel'den aktarilir) ─────────
    [ObservableProperty] private decimal _grandTotal;
    // Setter yeniden hesaplar: GrandTotal degismezse (ayni sepetle tekrar
    // odemeye gecis) OnGrandTotalChanged tetiklenmez, indirim kalan tutara
    // yansimiyordu.
    private decimal _subTotalDiscount;
    public decimal SubTotalDiscount
    {
        get => _subTotalDiscount;
        set
        {
            _subTotalDiscount = value;
            RecalculatePayments();
        }
    }
    public int? CustomerId { get; set; }
    public List<SaleItemRequest> SaleItems { get; set; } = new();

    /// <summary>Bu odeme oturumunun tekrar gonderim anahtari (sunucu ayni anahtarla tek islem yapar).</summary>
    public string IdempotencyKey { get; private set; } = Guid.NewGuid().ToString("N");

    // Satir indirimi varsa yonetici onay token'i (MainPosViewModel aktarir).
    public string? ApprovalToken { get; set; }

    // Fissiz iade varsa yonetici onay token'i ("return-no-receipt")
    public string? ReturnApprovalToken { get; set; }

    // Sepette IsReturnItem=true satir varsa (Iade-Degisim akisi) buraya
    // doluyor. Doluysa CompleteSaleAsync ProcessSaleAsync yerine
    // ProcessExchangeAsync'i cagirir.
    private List<ExchangeReturnItemRequest> _pendingReturnItems = new();
    public List<ExchangeReturnItemRequest> PendingReturnItems
    {
        get => _pendingReturnItems;
        set
        {
            _pendingReturnItems = value ?? new();
            OnPropertyChanged(nameof(IsExchange));
            RecalculatePayments();
        }
    }

    /// <summary>Bu odeme oturumu bir Iade-Degisim mi (sepette iade satiri var).</summary>
    public bool IsExchange => PendingReturnItems.Count > 0;

    /// <summary>
    /// Degisimde iade kredisi yeni satisi tam karsiliyor: ne tahsilat ne iade
    /// var - otomatik tamamlama tetiklenmedigi icin ayri "Tamamla" butonu gosterilir.
    /// </summary>
    public bool IsZeroBalanceExchange => IsExchange && RemainingAmount <= 0
                                         && AmountOwedToCustomer <= 0 && PaymentLines.Count == 0;

    public ObservableCollection<PaymentLine> PaymentLines { get; } = new();

    [ObservableProperty] private string _amountInput = string.Empty;

    [ObservableProperty] private decimal _paidTotal;
    [ObservableProperty] private decimal _remainingAmount;
    [ObservableProperty] private decimal _changeAmount;

    [ObservableProperty] private bool _isCouponPanelOpen;
    [ObservableProperty] private string _couponInput = string.Empty;
    [ObservableProperty] private decimal _couponDiscount;
    [ObservableProperty] private string? _appliedCouponCode;

    [ObservableProperty] private bool _isGiftCardPanelOpen;
    [ObservableProperty] private string _giftCardInput = string.Empty;
    [ObservableProperty] private decimal _giftCardBalance;
    [ObservableProperty] private bool _isGiftCardValid;

    // Doviz kurlari appsettings.json'dan (Currency bolumu) gelir.
    // "Parametre Guncelle" ile degisebilir - her kullanimda ayardan okunur.
    private readonly ClientSettings _settings;
    private decimal UsdRate => _settings.Currency.UsdRate;
    private decimal EurRate => _settings.Currency.EurRate;

    // ── POS (Nakit/Euro/POS akisi) ───────────────────────────
    // Fis ekraninda secilen vade farki (0=Pesin, 4, 8). MainPosViewModel'den
    // ResetPaymentState(lockedInstallment) ile aktarilir.
    [ObservableProperty] private int _lockedInstallment;

    [ObservableProperty] private bool _isPosMenuOpen;
    [ObservableProperty] private bool _isInstallmentMenuOpen;

    // LockedInstallment doluysa (fis ekraninda 4 veya 8 secilmisse) sadece o
    // secenek gosterilir; secilmemisse gercek banka taksitleri (2-6) gosterilir.
    public IEnumerable<int> AvailableInstallmentOptions =>
        LockedInstallment > 0 ? new[] { LockedInstallment } : new[] { 2, 3, 4, 5, 6 };

    // XAML'da BoolToVisibilityConverter ile kullanmak icin (int dogrudan baglanamaz)
    public bool HasLockedInstallment => LockedInstallment > 0;

    partial void OnLockedInstallmentChanged(int value)
    {
        OnPropertyChanged(nameof(AvailableInstallmentOptions));
        OnPropertyChanged(nameof(HasLockedInstallment));
    }

    public event Action<ProcessSaleResponse>? SaleCompleted;
    public event Action<ProcessExchangeResult>? ExchangeCompleted;
    public event Action? GoBackRequested;

    /// <summary>
    /// Otomatik tamamlama (Para Ustu overlay'i) BASARIYLA bitip bekleme
    /// suresi de dolduktan SONRA tetiklenir. MainPosViewModel bunu dinleyip
    /// IsPaymentOpen=false + ClearCart() yapar - boylece kapanma, Para Ustu
    /// ekraninin gorunur kaldigi sure boyunca ERKEN tetiklenmez.
    /// </summary>
    public event Action? ReadyToClose;

    public string CashierName => _authService.CurrentUser?.FullName ?? "Kasiyer";

    private readonly OfflineSaleQueue? _offlineQueue;

    public PaymentViewModel(PosService posService, AuthService authService, ClientSettings settings,
        OfflineSaleQueue? offlineQueue = null)
    {
        _posService = posService;
        _authService = authService;
        _settings = settings;
        _offlineQueue = offlineQueue;
        PaymentLines.CollectionChanged += (_, _) =>
        {
            RecalculatePayments();
            CheckAutoComplete();
        };
    }

    // GrandTotal degisince otomatik yeniden hesapla
    partial void OnGrandTotalChanged(decimal value)
    {
        RecalculatePayments();
        OnPropertyChanged(nameof(DocumentTotalLabel));
    }

    // lockedInstallment: fis ekranindaki MainPosViewModel.SelectedInstallment
    // degeri (0/4/8). MainPosViewModel.ProceedToPayment bunu geciriyor.
    public void ResetPaymentState(int lockedInstallment = 0)
    {
        PaymentLines.Clear();
        AmountInput = string.Empty;
        CouponDiscount = 0;
        AppliedCouponCode = null;
        IsCouponPanelOpen = false;
        CouponInput = string.Empty;
        IsGiftCardPanelOpen = false;
        GiftCardInput = string.Empty;
        GiftCardBalance = 0;
        IsGiftCardValid = false;
        IsPosMenuOpen = false;
        IsInstallmentMenuOpen = false;
        LockedInstallment = lockedInstallment;
        SubTotalDiscount = 0;
        ApprovalToken = null;
        ReturnApprovalToken = null;
        PendingReturnItems = new();
        _isCompleted = false;
        // Yeni odeme oturumu = yeni islem anahtari. Ayni oturumda (ag hatasi
        // sonrasi ✓'ye tekrar basma) AYNI anahtar gider: sunucu ikinci fis
        // kesmez, ilk islemin sonucunu dondurur.
        IdempotencyKey = Guid.NewGuid().ToString("N");
        _lastAmountOwedToCustomer = 0;
        OwedRefundMethod = RefundPayoutMethod.Cash;
        OwedGiftCardInput = string.Empty;
        IsParaUstuVisible = false;
        ClearError();
        RecalculatePayments();
    }

    [RelayCommand]
    private void AppendDigit(string digit)
        => AmountInput += digit;

    [RelayCommand]
    private void Backspace()
    {
        if (AmountInput.Length > 0)
            AmountInput = AmountInput[..^1];
    }

    [RelayCommand]
    private void ClearAmount()
        => AmountInput = string.Empty;

    /// <summary>
    /// Musteriye para iadesi varken (degisimde iade > yeni satis) tahsilat
    /// alinmaz - backend de reddeder. Odeme butonlari bu durumda uyarir.
    /// </summary>
    private bool RejectPaymentWhenRefund()
    {
        if (AmountOwedToCustomer <= 0) return false;
        SetError($"Musteriye {AmountOwedToCustomer:N2} TL iade edilecek; odeme alinmaz. " +
                 "Iade yontemini secip 'IADEYI TAMAMLA'ya basin.");
        return true;
    }

    /// <summary>XAML: odeme yontemi butonlari sadece tahsilat varken aktif.</summary>
    public bool CanTakePayment => AmountOwedToCustomer <= 0;

    /// <summary>XAML: uygulanmis kupon var mi.</summary>
    public bool HasCoupon => !string.IsNullOrEmpty(AppliedCouponCode);
    partial void OnAppliedCouponCodeChanged(string? value) => OnPropertyChanged(nameof(HasCoupon));

    /// <summary>XAML: belge toplami eksiyse "Musteriye Iade" etiketi.</summary>
    public string DocumentTotalLabel => GrandTotal < 0 ? "Belge toplami (iade)" : "Belge toplami";

    [RelayCommand]
    private void AddCash()
    {
        if (RejectPaymentWhenRefund()) return;
        // GUVENLIK: Kasiyer yanlislikla bu butona basip TUM kalan tutari
        // cekmesin diye - artik RemainingAmount'a otomatik fallback YOK,
        // once Numpad'den gercek bir tutar girilmis olmasi ZORUNLU.
        var amount = ParseAmount();
        if (amount <= 0)
        {
            SetError("Once bir tutar girin.");
            return;
        }

        PaymentLines.Add(new PaymentLine
        {
            Label = "Nakit",
            Amount = amount,
            Type = PaymentType.Cash
        });
        AmountInput = string.Empty;
        RecalculatePayments();
    }

    /// <summary>
    /// Normal odeme ekraninda "Gider Pusulasi" ile tahsilat - AddCash ile
    /// birebir ayni mantik, sadece etiket farkli. (Daha once bu komutu
    /// iade-sonrasi FARK ODEME panelinde yanlis kullanmistik - orada negatif
    /// net tutari "toplama" gibi ele alip hesabi bozuyordu. Burada ise normal
    /// pozitif tahsilat baglaminda, digerleri gibi guvenli.)
    /// </summary>
    [RelayCommand]
    private void AddGiderPusulasi()
    {
        if (RejectPaymentWhenRefund()) return;
        // GUVENLIK: AddCash ile ayni kural - otomatik RemainingAmount
        // fallback yok, tutar elle girilmis olmali.
        var amount = ParseAmount();
        if (amount <= 0)
        {
            SetError("Once bir tutar girin.");
            return;
        }

        PaymentLines.Add(new PaymentLine
        {
            Label = "Gider Pusulasi",
            Amount = amount,
            Type = PaymentType.Cash
        });
        AmountInput = string.Empty;
        RecalculatePayments();
    }

    [RelayCommand]
    private void AddUsd()
    {
        if (RejectPaymentWhenRefund()) return;
        var usdAmount = ParseAmount();
        if (usdAmount <= 0) { SetError("Once USD tutarini girin."); return; }
        var tryAmount = Math.Round(usdAmount * UsdRate, 2);

        PaymentLines.Add(new PaymentLine
        {
            Label = $"USD ({usdAmount:F2} x {UsdRate})",
            Amount = tryAmount,
            Type = PaymentType.Cash
        });
        AmountInput = string.Empty;
        RecalculatePayments();
    }

    [RelayCommand]
    private void AddEur()
    {
        if (RejectPaymentWhenRefund()) return;
        var eurAmount = ParseAmount();
        if (eurAmount <= 0) { SetError("Once EUR tutarini girin."); return; }
        var tryAmount = Math.Round(eurAmount * EurRate, 2);

        PaymentLines.Add(new PaymentLine
        {
            Label = $"EUR ({eurAmount:F2} x {EurRate})",
            Amount = tryAmount,
            Type = PaymentType.Cash
        });
        AmountInput = string.Empty;
        RecalculatePayments();
    }

    // ── POS Akisi: Satis (tek cekim) / Taksit ────────────────
    [RelayCommand]
    private void OpenPosMenu()
    {
        if (RejectPaymentWhenRefund()) return;
        IsPosMenuOpen = true;
        IsInstallmentMenuOpen = false;
    }

    [RelayCommand]
    private void ClosePosMenu()
    {
        IsPosMenuOpen = false;
        IsInstallmentMenuOpen = false;
    }

    [RelayCommand]
    private void PosSale()
    {
        AddCardPayment(installmentCount: 0, labelSuffix: "Satis");
        ClosePosMenu();
    }

    [RelayCommand]
    private void OpenInstallmentMenu()
    {
        IsPosMenuOpen = false;
        IsInstallmentMenuOpen = true;
    }

    [RelayCommand]
    private void SelectPosInstallment(int value)
    {
        if (value <= 0) return;
        AddCardPayment(installmentCount: value, labelSuffix: $"{value} Taksit");
        ClosePosMenu();
    }

    private void AddCardPayment(int installmentCount, string labelSuffix)
    {
        if (RemainingAmount <= 0)
        {
            SetError("Kalan tutar yok.");
            return;
        }

        var amount = ParseAmount();
        if (amount <= 0) amount = RemainingAmount;

        // Karttan kalan tutardan fazlasi cekilemez (para ustu sadece nakitten
        // verilir - backend de fazla kart odemesini reddeder).
        if (amount > RemainingAmount)
        {
            SetError($"Karttan en fazla kalan tutar ({RemainingAmount:F2} TL) cekilebilir.");
            return;
        }

        PaymentLines.Add(new PaymentLine
        {
            Label = $"POS ({labelSuffix})",
            Amount = amount,
            Type = PaymentType.Card,
            InstallmentCount = installmentCount > 0 ? installmentCount : null
        });
        AmountInput = string.Empty;
        RecalculatePayments();
    }

    [RelayCommand]
    private void RemovePaymentLine(PaymentLine line)
    {
        PaymentLines.Remove(line);
        RecalculatePayments();
    }

    [RelayCommand]
    private void OpenCouponPanel()
    {
        // Degisimde kupon desteklenmiyor (iade kredisiyle birlikte hangi tutara
        // uygulanacagi tanimsiz) - backend de exchange'de kupon kabul etmez.
        if (PendingReturnItems.Count > 0)
        {
            SetError("Iade-Degisim isleminde kupon kullanilamaz.");
            return;
        }
        if (PaymentLines.Any())
        {
            SetError("Kupon, odeme alinmaya baslanmadan once uygulanmalidir.");
            return;
        }
        CouponInput = string.Empty;
        IsCouponPanelOpen = true;
    }

    [RelayCommand]
    private async Task ApplyCouponAsync()
    {
        if (string.IsNullOrWhiteSpace(CouponInput)) return;

        try
        {
            IsBusy = true;
            ClearError();

            // Kupon, ara toplam indiriminden SONRAKI tutara uygulanir -
            // backend ProcessSaleCommandHandler ile ayni sira.
            var baseAmount = GrandTotal - SubTotalDiscount;
            var result = await _posService.ValidateCouponAsync(CouponInput.Trim(), baseAmount);

            CouponDiscount = result.DiscountAmount;
            AppliedCouponCode = result.Code;
            IsCouponPanelOpen = false;
            CouponInput = string.Empty;
            RecalculatePayments();
        }
        catch (ApiException ex)
        {
            SetError($"Kupon gecersiz: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetError($"Kupon dogrulanamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelCoupon()
    {
        IsCouponPanelOpen = false;
        CouponInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveCoupon()
    {
        CouponDiscount = 0;
        AppliedCouponCode = null;
        RecalculatePayments();
    }

    [RelayCommand]
    private void OpenGiftCardPanel()
    {
        if (RejectPaymentWhenRefund()) return;
        ClearError();
        GiftCardInput = string.Empty;
        GiftCardBalance = 0;
        IsGiftCardValid = false;
        IsGiftCardPanelOpen = true;
    }

    [RelayCommand]
    private async Task QueryGiftCardAsync()
    {
        if (string.IsNullOrWhiteSpace(GiftCardInput)) return;
        try
        {
            IsBusy = true;
            ClearError();
            var card = await _posService.GetGiftCardAsync(GiftCardInput.Trim());
            if (card.ExpiryDate.Date < DateTime.Today)
            {
                SetError($"Hediye kartinin suresi dolmus ({card.ExpiryDate:dd.MM.yyyy}).");
                IsGiftCardValid = false;
            }
            else if (card.IsActive && card.Balance > 0)
            {
                GiftCardBalance = card.Balance;
                IsGiftCardValid = true;
            }
            else
            {
                SetError("Gecersiz veya bakiyesiz hediye karti.");
                IsGiftCardValid = false;
            }
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            SetError("Hediye karti bulunamadi.");
            IsGiftCardValid = false;
        }
        catch (Exception ex)
        {
            SetError($"Hediye karti sorgulanamadi: {ex.Message}");
            IsGiftCardValid = false;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddGiftCard()
    {
        if (!IsGiftCardValid) return;

        // Ayni kart bu odemede zaten kullanildiysa, o tutar bakiyeden dusulur.
        var alreadyUsed = PaymentLines
            .Where(l => l.Type == PaymentType.GiftCard && l.CardNumber == GiftCardInput)
            .Sum(l => l.Amount);
        var amount = Math.Min(GiftCardBalance - alreadyUsed, RemainingAmount);
        if (amount <= 0)
        {
            SetError(RemainingAmount <= 0
                ? "Kalan tutar yok."
                : "Bu hediye kartinin bakiyesi bu odemede zaten kullanildi.");
            return;
        }

        PaymentLines.Add(new PaymentLine
        {
            Label = $"Hediye Karti ({GiftCardInput})",
            Amount = amount,
            Type = PaymentType.GiftCard,
            CardNumber = GiftCardInput
        });
        IsGiftCardPanelOpen = false;
        GiftCardInput = string.Empty;
        RecalculatePayments();
    }

    [RelayCommand]
    private void CancelGiftCard()
    {
        IsGiftCardPanelOpen = false;
        GiftCardInput = string.Empty;
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        // Islem suruyorsa ya da bu odeme oturumunda zaten basariyla
        // tamamlandiysa ikinci kez backend'e gonderme (cift satis/degisim).
        if (_isCompleting || _isCompleted) return;

        if (RemainingAmount > 0)
        {
            SetError("Odeme eksik. Kalan tutari tamamlayin.");
            return;
        }

        var isExchange = PendingReturnItems.Count > 0;
        if (!PaymentLines.Any() && !isExchange)
        {
            SetError("En az bir odeme yontemi ekleyin.");
            return;
        }

        if (AmountOwedToCustomer > 0)
        {
            if (PaymentLines.Any())
            {
                SetError("Musteriye para iadesi varken odeme alinmaz. Odeme satirlarini kaldirin.");
                return;
            }
            if (OwedRefundMethod == RefundPayoutMethod.GiftCard && string.IsNullOrWhiteSpace(OwedGiftCardNumber))
            {
                SetError("Hediye kartina iade icin kart numarasi girin.");
                return;
            }
            if (OwedRefundMethod == RefundPayoutMethod.GiftCard && OwedGiftCardNumber.Trim().Length < 4)
            {
                SetError("Hediye karti numarasi gecersiz.");
                return;
            }
        }

        // Manuel (✓) tamamlama da otomatik tamamlama ile AYNI yoldan gecer -
        // boylece ReadyToClose (ekrani kapatip sepeti temizleyen sinyal) her
        // basarili tamamlamada tetiklenir.
        await RunCompletionAsync();
    }

    /// <summary>
    /// Gercek backend tamamlama cagrisi - sadece RunCompletionAsync
    /// tarafindan cagrilir (hem manuel ✓ hem otomatik tamamlama).
    /// </summary>
    private async Task<bool> CompleteSaleCoreAsync()
    {
        try
        {
            IsBusy = true;
            ClearError();

            var payments = PaymentLines.Select(p => new SalePaymentRequest(
                (int)p.Type,
                p.Amount,
                p.InstallmentCount,
                p.CardNumber
            )).ToList();

            if (PendingReturnItems.Count > 0)
            {
                // ── Iade-Degisim: TEK atomik ProcessExchangeAsync cagrisi ──
                var exchangeRequest = new ProcessExchangeRequest(
                    ReturnItems: PendingReturnItems,
                    NewItems: SaleItems,
                    SettlementPayments: payments,
                    CustomerId: CustomerId,
                    // Normal satisla ayni: ara toplam + kupon indirimi ve satir
                    // indirimi icin yonetici onay token'i backend'e gider.
                    DiscountAmount: SubTotalDiscount,
                    ApprovalToken: ApprovalToken,
                    InstallmentPlan: LockedInstallment > 0 ? LockedInstallment : null,
                    // Musteriye kalan fark AYNI islemde secilen yontemle odenir
                    // ve iade defterine yazilir (kasa sayimi nakit iadeyi gorur).
                    OwedRefundMethod: AmountOwedToCustomer > 0 ? (int)OwedRefundMethod : null,
                    OwedGiftCardNumber: AmountOwedToCustomer > 0 && OwedRefundMethod == RefundPayoutMethod.GiftCard
                        ? OwedGiftCardNumber.Trim()
                        : null,
                    ReturnApprovalToken: ReturnApprovalToken
                );

                var result = await _posService.ProcessExchangeAsync(exchangeRequest, IdempotencyKey);
                _lastAmountOwedToCustomer = result.AmountOwedToCustomer;
                ExchangeCompleted?.Invoke(result);
            }
            else
            {
                // ── Normal satis: mevcut akis, degismedi ──
                var request = new ProcessSaleRequest(
                    Items: SaleItems,
                    Payments: payments,
                    // Backend ProcessSaleCommand.DiscountAmount non-nullable decimal (default 0).
                    // null gonderilirse JSON deserialize hatasi olur; bu yuzden asla null gonderme.
                    // Kupon indirimi backend'de CouponCode'dan hesaplanir;
                    // burada sadece ara toplam indirimi gonderilir.
                    DiscountAmount: SubTotalDiscount,
                    CustomerId: CustomerId,
                    ApprovalToken: ApprovalToken,
                    CouponCode: AppliedCouponCode,
                    InstallmentPlan: LockedInstallment > 0 ? LockedInstallment : null
                );

                ProcessSaleResponse response;
                if (!ConnectivityMonitor.Instance.IsOnline && CanSellOffline(out _))
                {
                    // Baglanti zaten yok: zaman asimi beklemeden cevrimdisi kaydet
                    response = SaveOffline(request);
                }
                else
                {
                    try
                    {
                        response = await _posService.ProcessSaleAsync(request, IdempotencyKey);
                    }
                    catch (ApiException ex) when (ex.StatusCode == 0)
                    {
                        if (!CanSellOffline(out var reason))
                        {
                            SetError($"{ex.Message} Cevrimdisi satis yapilamaz: {reason}");
                            return false;
                        }
                        // AYNI islem anahtariyla kuyruga: istek aslinda sunucuya ulasip
                        // islendiyse gonderimde ayni fis doner, cift satis olusmaz.
                        response = SaveOffline(request);
                    }
                }
                SaleCompleted?.Invoke(response);
            }
            return true;
        }
        catch (ApiException ex)
        {
            // Backend'in kendi mesajini goster (orn. "Indirim uygulamak icin
            // yonetici onayi gereklidir.") - genel "Yetkisiz islem." kasiyere
            // sebebi soylemiyordu.
            SetError(ex.StatusCode == 401
                ? $"Yetkisiz islem: {ex.Message}"
                : $"Satis hatasi: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            SetError($"Hata: {ex.Message}");
            return false;
        }
        finally { IsBusy = false; }
    }

    // ── Otomatik Tamamlama + Para Ustu Ekrani ──────────────────────
    [ObservableProperty] private bool _isParaUstuVisible;
    [ObservableProperty] private string _paraUstuText = string.Empty;
    private bool _isCompleting;

    // Bu odeme oturumunda backend islemi basariyla kaydetti mi? True iken
    // hicbir yol (otomatik/manuel) islemi tekrar gondermez. ResetPaymentState
    // (yeni odeme oturumu) ile sifirlanir.
    private bool _isCompleted;

    // Son degisimde musteriye odenecek fark (varsa Para Ustu bekleme suresi
    // atlanir - o tutar MainPosViewModel'in Fark Odeme panelinde islenir).
    private decimal _lastAmountOwedToCustomer;

    /// <summary>
    /// Her odeme ekleme metodundan (AddCash, AddUsd, vb.) RecalculatePayments()
    /// sonrasinda cagrilir. KalanTutar sifira/altina duserse otomatik
    /// tamamlamayi baslatir - kasiyer ayrica bir butona basmak zorunda kalmaz.
    /// Sadece EN AZ BIR odeme satiri varken (ya da bekleyen iade varken)
    /// tetiklenir - boylece Odeme ekrani ilk acildigi anda (RemainingAmount
    /// bastan 0 olabilecek saf iade senaryosunda) beklenmedik sekilde
    /// aninda tetiklenmez, kasiyer o durumda hala manuel onaylar (✓ tusu).
    /// </summary>
    private void CheckAutoComplete()
    {
        if (_isCompleting || _isCompleted) return;
        if (RemainingAmount > 0) return;
        if (!PaymentLines.Any()) return;

        _ = RunCompletionAsync();
    }

    /// <summary>
    /// Tek tamamlama yolu: backend cagrisi + Para Ustu ekrani + ReadyToClose.
    /// Hem otomatik tamamlama hem manuel ✓ buradan gecer.
    /// </summary>
    private async Task RunCompletionAsync()
    {
        if (_isCompleting || _isCompleted) return;
        _isCompleting = true;

        try
        {
            // Para ustunu hemen hesaplayip goster (backend cevabini beklemeden)
            // Musteriye iade varsa (degisimde iade > yeni satis) o tutar ve
            // yontemi gosterilir; yoksa para ustu.
            ParaUstuText = AmountOwedToCustomer > 0
                ? $"Iade ({OwedRefundLabel}): {AmountOwedToCustomer:F2} TL"
                : $"-{ChangeAmount:F2} TL";
            IsParaUstuVisible = true;

            var success = await CompleteSaleCoreAsync();

            if (success)
            {
                _isCompleted = true;

                // Kasiyerin para ustunu / nakit iadeyi kasadan rahatca verebilmesi
                // icin 6.5 saniye bekle (iade artik islemin icinde odeniyor).
                await Task.Delay(6500);
                IsParaUstuVisible = false;
                // Kapanma sinyali SADECE burada, bekleme bittikten SONRA tetiklenir -
                // boylece MainPosViewModel IsPaymentOpen'i erken kapatip Para Ustu
                // ekranini gormeden ekrani degistirmez.
                ReadyToClose?.Invoke();
            }
            else
            {
                // Basarisiz oldu - hata zaten SetError ile gosterildi, overlay hemen
                // kapanir ki kasiyer hatayi gorup Odeme ekraninda duzeltebilsin.
                IsParaUstuVisible = false;
            }
        }
        finally
        {
            _isCompleting = false;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        // Islem gonderiliyorken ya da kaydedildikten sonra geri donulmez -
        // aksi halde kaydedilmis satisin sepeti Fis ekraninda tekrar
        // odemeye acilabilir.
        if (_isCompleting || _isCompleted) return;
        GoBackRequested?.Invoke();
    }

    private void RecalculatePayments()
    {
        var netTotal = GrandTotal - SubTotalDiscount - CouponDiscount;
        PaidTotal = PaymentLines.Sum(p => p.Amount);
        RemainingAmount = Math.Max(0, netTotal - PaidTotal);
        // Para ustu sadece alinmasi gereken pozitif tutar asildiginda (iade
        // farkinda net eksi iken odeme alinmaz - o tutar musteriye iadedir).
        ChangeAmount = PaidTotal > Math.Max(0, netTotal) ? Math.Round(PaidTotal - Math.Max(0, netTotal), 2) : 0;
        AmountOwedToCustomer = netTotal < 0 ? Math.Round(-netTotal, 2) : 0;
        OnPropertyChanged(nameof(IsZeroBalanceExchange));
    }

    // ── Musteriye iade (Iade-Degisimde iade > yeni satis) ──────────
    // Odeme ekraninda, islem TAMAMLANMADAN secilir; backend ayni transaction'da
    // iade defterine yazar. (Eskiden islem bittikten sonra ayri bir "Fark Odeme"
    // panelinde sadece not dusuluyordu - nakit iade kasa sayiminda gorunmuyordu.)
    [ObservableProperty] private decimal _amountOwedToCustomer;
    [ObservableProperty] private RefundPayoutMethod _owedRefundMethod = RefundPayoutMethod.Cash;
    [ObservableProperty] private string _owedGiftCardInput = string.Empty;

    public bool HasAmountOwed => AmountOwedToCustomer > 0;
    public string OwedRefundLabel => OwedRefundMethod switch
    {
        RefundPayoutMethod.Card => "Karta",
        RefundPayoutMethod.GiftCard => "Hediye Kartina",
        _ => "Nakit"
    };
    public bool IsOwedCash => OwedRefundMethod == RefundPayoutMethod.Cash;
    public bool IsOwedCard => OwedRefundMethod == RefundPayoutMethod.Card;
    public bool IsOwedGiftCard => OwedRefundMethod == RefundPayoutMethod.GiftCard;
    private string OwedGiftCardNumber => OwedGiftCardInput ?? string.Empty;

    partial void OnAmountOwedToCustomerChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasAmountOwed));
        OnPropertyChanged(nameof(CanTakePayment));
    }

    partial void OnOwedRefundMethodChanged(RefundPayoutMethod value)
    {
        OnPropertyChanged(nameof(IsOwedCash));
        OnPropertyChanged(nameof(IsOwedCard));
        OnPropertyChanged(nameof(IsOwedGiftCard));
        OnPropertyChanged(nameof(OwedRefundLabel));
    }

    [RelayCommand]
    private void SelectOwedRefundMethod(string method)
    {
        OwedRefundMethod = method switch
        {
            "Card" => RefundPayoutMethod.Card,
            "GiftCard" => RefundPayoutMethod.GiftCard,
            _ => RefundPayoutMethod.Cash
        };
        ClearError();
    }

    // ── Cevrimdisi satis ───────────────────────────────────────────
    /// <summary>
    /// Sunucu onayi gerektiren hicbir sey yoksa satis cevrimdisi yapilabilir:
    /// kupon, hediye karti, indirim (yonetici onayi) ve degisim/iade haric.
    /// </summary>
    public bool CanSellOffline(out string reason)
    {
        reason = string.Empty;
        if (_offlineQueue is null || !_settings.Offline.Enabled) reason = "cevrimdisi satis kapali.";
        else if (_authService.CurrentUser is null) reason = "oturum yok.";
        else if (PendingReturnItems.Count > 0) reason = "degisim/iade sunucu baglantisi gerektirir.";
        else if (!string.IsNullOrEmpty(AppliedCouponCode)) reason = "kupon kullanilamaz; kuponu kaldirin.";
        else if (PaymentLines.Any(p => p.Type == PaymentType.GiftCard)) reason = "hediye karti ile odeme alinamaz.";
        else if (SubTotalDiscount > 0 || !string.IsNullOrEmpty(ApprovalToken) || SaleItems.Any(i => i.DiscountValue is > 0))
            reason = "indirim uygulanamaz (yonetici onayi sunucu gerektirir).";
        else if (SaleItems.Count == 0 || SaleItems.Any(i => i.OfflineUnitPrice is null or <= 0))
            reason = "urun fiyati bilinmiyor.";
        return reason.Length == 0;
    }

    private ProcessSaleResponse SaveOffline(ProcessSaleRequest request)
    {
        var user = _authService.CurrentUser!;
        var soldAt = DateTime.UtcNow;
        var reference = _offlineQueue!.NextReference(user.StoreId, DateTime.Now);
        var finalAmount = GrandTotal - SubTotalDiscount - CouponDiscount;
        _offlineQueue.Enqueue(new OfflineSale
        {
            Id = IdempotencyKey,
            Reference = reference,
            StoreId = user.StoreId,
            CashierPersonelId = user.PersonelId,
            CashierName = user.FullName,
            SoldAtUtc = soldAt,
            FinalAmount = finalAmount,
            Request = request with { Offline = new OfflineSaleInfo(reference, soldAt) }
        });
        return new ProcessSaleResponse(true, reference, finalAmount, DateTime.Now, null, IsOffline: true);
    }

    private decimal ParseAmount()
    {
        var input = AmountInput.Replace(",", ".");
        return decimal.TryParse(input,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) ? v : 0;
    }
}