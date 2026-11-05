using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Collections.ObjectModel;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class MainPosViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ProductService _productService;
    private readonly PosService _posService;
    private readonly ApiClient _apiClient;
    private readonly PaymentViewModel _paymentViewModel;
    private readonly ReceiptService _receiptService;
    private readonly CustomerSearchViewModel _customerSearchViewModel;

    public ObservableCollection<CartItem> CartItems { get; } = new();

    [ObservableProperty] private CartItem? _selectedCartItem;

    /// <summary>Sag ust urun karti bos/dolu durumunu belirlemek icin (XAML BoolToVisibilityConverter ile).</summary>
    public bool HasSelectedCartItem => SelectedCartItem is not null;
    partial void OnSelectedCartItemChanged(CartItem? value)
        => OnPropertyChanged(nameof(HasSelectedCartItem));
    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _successMessage;

    [ObservableProperty] private decimal _finalAmount;
    [ObservableProperty] private int _lineCount;
    [ObservableProperty] private decimal _installment4;
    [ObservableProperty] private decimal _installment8;
    [ObservableProperty] private decimal _netTotal;

    [ObservableProperty] private int _selectedInstallment = 0;

    public string Installment4Label => SelectedInstallment == 4 ? "Pesin Fiyat" : "4 Taksit  +%5";
    public string Installment8Label => SelectedInstallment == 8 ? "Pesin Fiyat" : "8 Taksit  +%10";

    public string CashierName => _authService.CurrentUser?.FullName ?? "Kasiyer";

    public PaymentViewModel PaymentVM => _paymentViewModel;
    [ObservableProperty] private bool _isPaymentOpen;

    /// <summary>
    /// Alt bardaki Ulke/Musteri Sec/Personel Arama/Belge Iptal/Islem Secimi
    /// grubu SADECE bu true iken gorunur - Ara Toplam ya da Odeme ekrani
    /// acikken bu buton grubu tamamen gizlenir.
    /// </summary>
    public bool IsOnFisScreen => !IsSubTotalOpen && !IsPaymentOpen;
    partial void OnIsPaymentOpenChanged(bool value)
        => OnPropertyChanged(nameof(IsOnFisScreen));
    partial void OnIsSubTotalOpenChanged(bool value)
        => OnPropertyChanged(nameof(IsOnFisScreen));

    public CustomerSearchViewModel CustomerSearchVM => _customerSearchViewModel;

    /// <summary>Urun Bul / Fiyat Gor paneli: urun kodu -> renk -> beden -> sepete ekle.</summary>
    public ProductLookupViewModel LookupVM { get; }

    internal static readonly System.Text.RegularExpressions.Regex ProductCodePattern =
        new System.Text.RegularExpressions.Regex("^[WS][0-9][A-Z]{2}[0-9A-Z]{2}Z[148]$");

    [RelayCommand]
    private void OpenProductLookup()
    {
        IsHamburgerMenuOpen = false;
        if (IsPaymentOpen || IsSubTotalOpen) { SetError("Urun eklemek icin Fis ekranina donun."); return; }
        var q = BarcodeInput.Trim();
        BarcodeInput = string.Empty;
        LookupVM.Open(q);
    }
    [ObservableProperty] private bool _isCustomerSearchOpen;
    public int? SelectedCustomerId { get; private set; }

    public event Action? OpenExchangeRequested;

    [ObservableProperty] private bool _isDiscountPanelOpen;
    [ObservableProperty] private string _discountInput = string.Empty;
    [ObservableProperty] private bool _isManagerDiscountPanelOpen;
    [ObservableProperty] private string _managerDiscountInput = string.Empty;
    [ObservableProperty] private bool _isHamburgerMenuOpen;

    [ObservableProperty] private bool _isSubTotalOpen;
    [ObservableProperty] private decimal _subTotalDiscountAmount;
    [ObservableProperty] private string? _subTotalDiscountLabel;
    [ObservableProperty] private bool _isSubDiscountPanelOpen;
    [ObservableProperty] private string _subDiscountInput = string.Empty;
    [ObservableProperty] private int _subDiscountMode;

    // Yuzde ara toplam indirimi uygulandiysa yuzde degeri (sepet degisince
    // tutar yeniden hesaplanir); tutar indiriminde null.
    private decimal? _subDiscountPercent;

    public MainPosViewModel(
        AuthService authService,
        ProductService productService,
        PosService posService,
        ApiClient apiClient,
        PaymentViewModel paymentViewModel,
        ReceiptService receiptService,
        CustomerSearchViewModel customerSearchViewModel,
        ProductLookupViewModel lookupViewModel,
        SuspendedCartService suspendedCartService,
        CashDrawerService cashDrawerService,
        LCWaikiki.POS.WpfClient.Core.ClientSettings settings)
    {
        _suspendedCartService = suspendedCartService;
        LookupVM = lookupViewModel;
        LookupVM.AddToCartRequested += async barcode =>
        {
            await AddProductByBarcodeAsync(barcode);
            if (string.IsNullOrEmpty(ErrorMessage))
                SuccessMessage = $"Sepete eklendi: {CartItems.LastOrDefault(i => i.Barcode == barcode)?.ProductName} ({barcode})";
        };
        _cashDrawerService = cashDrawerService;
        _settings = settings;
        CustomerCountry = settings.Countries.FirstOrDefault() ?? "Turkiye";
        _authService = authService;
        _productService = productService;
        _posService = posService;
        _apiClient = apiClient;
        _paymentViewModel = paymentViewModel;
        _receiptService = receiptService;
        _customerSearchViewModel = customerSearchViewModel;

        CartItems.CollectionChanged += (_, _) => RecalculateTotals();

        _paymentViewModel.GoBackRequested += () => IsPaymentOpen = false;
        _paymentViewModel.SaleCompleted += OnSaleCompleted;
        _paymentViewModel.ExchangeCompleted += OnExchangeSaleCompleted;
        _paymentViewModel.ReadyToClose += OnPaymentReadyToClose;

        _customerSearchViewModel.CustomerSelected += OnCustomerSelected;
        _customerSearchViewModel.CancelRequested += () => IsCustomerSearchOpen = false;
    }

    private void OnCustomerSelected(CustomerDto customer)
    {
        SelectedCustomerId = customer.Id;
        CustomerName = customer.FullName;
        IsCustomerSearchOpen = false;
        ClearError();
    }

    [RelayCommand]
    private void OpenExchange()
    {
        IsHamburgerMenuOpen = false;
        // Odeme/Ara Toplam acikken Iade-Degisim'e gecilmez: odeme yarida
        // kalir ve geri donuste sepet ile odeme ekrani tutarsiz olurdu.
        if (IsPaymentOpen || IsSubTotalOpen)
        {
            SetError("Iade-Degisim icin once Ara Toplam/Odeme ekranindan Fis ekranina geri donun.");
            return;
        }
        OpenExchangeRequested?.Invoke();
    }

    /// <summary>
    /// IadeDegisimWindow'dan "Satisa Gec" ile aktarilan iade kalemlerini
    /// gercek sepete (CartItem, IsReturnItem=true) ekler.
    /// </summary>
    public void AttachPendingReturns(List<ExchangeReturnItem> returnItems, CustomerDto? customer = null)
    {
        // Iade-Degisim ekraninda secilen musteri (orn. Numarayla iade) Fis
        // ekranina tasinir - yeni satis da ayni musteriye yazilsin. Fis
        // ekraninda zaten musteri seciliyse o korunur.
        if (customer is not null && SelectedCustomerId is null)
        {
            SelectedCustomerId = customer.Id;
            CustomerName = customer.FullName;
        }

        foreach (var item in returnItems)
        {
            // Ayni fisten ayni urunun iadesi Fis ekraninda zaten bekliyorsa
            // (ve ayni nedenle) satir birlestirilir.
            if (!item.IsSaleItem)
            {
                var sameReturn = CartItems.FirstOrDefault(c =>
                    c.IsReturnItem && !c.IsVoided
                    && c.VariantId == item.VariantId
                    && c.SourceReceiptNumber == item.ReceiptNumber
                    && c.UnitPrice == item.UnitPrice
                    && c.ReturnReasonId == item.ReturnReasonId);
                if (sameReturn is not null)
                {
                    sameReturn.Quantity += item.Quantity;
                    continue;
                }
            }
            else
            {
                var sameSale = CartItems.FirstOrDefault(c =>
                    !c.IsReturnItem && !c.IsVoided && c.VariantId == item.VariantId);
                if (sameSale is not null)
                {
                    sameSale.Quantity += item.Quantity;
                    continue;
                }
            }

            CartItems.Add(new CartItem
            {
                VariantId = item.VariantId,
                Barcode = item.Barcode,
                ProductName = item.ProductName,
                Color = item.Color,
                Size = item.Size,
                ImagePath = item.ImagePath,
                FullImageUrl = item.FullImageUrl,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                IsReturnItem = !item.IsSaleItem,
                SourceReceiptNumber = item.ReceiptNumber,
                ReturnReasonId = item.ReturnReasonId,
                ReturnReasonName = item.ReturnReasonName,
                // Fis ekraninda taksit seciliyse yeni satis satirlarina da uygulanir
                // (backend vade farkini tum yeni satis satirlarina ekler).
                InstallmentCount = item.IsSaleItem ? SelectedInstallment : 0
            });
        }

        RecalculateTotals();
        ClearError();
        var returnCount = returnItems.Where(i => !i.IsSaleItem).Sum(i => i.Quantity);
        var saleCount = returnItems.Where(i => i.IsSaleItem).Sum(i => i.Quantity);
        SuccessMessage = (saleCount > 0
            ? $"{returnCount} iade + {saleCount} yeni urun aktarildi. Yeni urun okutup ARA TOPLAM ile devam edin."
            : $"{returnCount} iade urunu aktarildi. Degisim icin yeni urun okutun ya da ARA TOPLAM ile iadeyi tamamlayin.");
    }

    /// <summary>
    /// Fis sepetinde bekleyen (iptal edilmemis) iade satirlari - Iade-Degisim
    /// penceresi acilirken ayni fisten kalan adedin asilmamasi icin aktarilir.
    /// </summary>
    public List<(string? ReceiptNumber, int VariantId, int Quantity)> GetPendingReturnQuantities()
        => CartItems
            .Where(i => i.IsReturnItem && !i.IsVoided)
            .Select(i => (i.SourceReceiptNumber, i.VariantId, i.Quantity))
            .ToList();

    private async void OnSaleCompleted(ProcessSaleResponse response)
    {
        // NOT: IsPaymentOpen=false ve ClearCart() ARTIK BURADA YAPILMIYOR.
        // PaymentViewModel'in Para Ustu overlay'i hala gorunur olabilir -
        // erken kapatirsak kasiyer para ustunu goremeden ekran degisir.
        // Asil kapatma, bekleme suresi bittikten sonra OnPaymentReadyToClose'da.
        try
        {
            var soldItems = CartItems.Where(i => !i.IsVoided).ToList();

            var receiptData = new ReceiptData(
                ReceiptNumber: response.ReceiptNumber,
                StoreLabel: $"Magaza {_authService.CurrentUser?.StoreId}",
                CashierName: CashierName,
                SaleDate: response.SaleDate,
                Items: soldItems.Select(i => new ReceiptLineData(
                    i.ProductName, i.Barcode, i.Color, i.Size,
                    i.Quantity, i.UnitPrice, i.DiscountAmount, i.LineTotal, i.InstallmentSurchargeAmount
                )).ToList(),
                // Satir indirimleri/vade farki satirlarda gosterilir; Ara Toplam
                // satir toplamlaridir, Indirim = ara toplam indirimi + kupon
                // (Ara Toplam - Indirim = Toplam tutarli kalir).
                TotalAmount: soldItems.Sum(i => i.LineTotal),
                DiscountAmount: soldItems.Sum(i => i.LineTotal) - response.FinalAmount,
                FinalAmount: response.FinalAmount,
                Payments: PaymentVM.PaymentLines.Select(p =>
                    new ReceiptPaymentData(p.Label, p.Amount)).ToList(),
                ChangeAmount: PaymentVM.ChangeAmount,
                HeaderLines: BuildHeaderLines()
            );

            // Fis sessizce kaydedilir - kasiyerin dosya yoluyla ilgilenmesine
            // gerek yok, ekstra bir onay/TAMAM popup'i cikmaz.
            await _receiptService.GenerateAndSaveReceiptAsync(receiptData);
        }
        catch (Exception ex)
        {
            SetError($"Fis PDF kaydedilemedi: {ex.GetType().Name} - {ex.Message}");
        }
    }

    /// <summary>
    /// PaymentViewModel.ReadyToClose tetiklenince cagrilir (Para Ustu
    /// ekrani bekleme suresini tamamladiktan SONRA). Asil kapanma/temizleme
    /// burada olur - boylece Para Ustu ekrani goruntuye tam suresince cikar.
    /// </summary>
    private void OnPaymentReadyToClose()
    {
        IsPaymentOpen = false;
        ClearCart();

        // Degisimde musteriye odenecek fark varsa, kasiyerin bunu nasil
        // kapattigini secmesi icin Fark Odeme paneli acilir.
        if (PendingSettlementAmount > 0)
        {
            SettlementGiftCardInput = string.Empty;
            IsSettlementPanelOpen = true;
        }
    }

    // ── Fark Odeme Paneli (musteriye para iadesi varsa, satis/degisim
    // tamamlandiktan sonra nasil odendigini kaydetmek icin) ──
    [ObservableProperty] private bool _isSettlementPanelOpen;
    [ObservableProperty] private decimal _pendingSettlementAmount;
    [ObservableProperty] private string _settlementGiftCardInput = string.Empty;

    /// <summary>
    /// PaymentViewModel.ExchangeCompleted tetiklenince cagrilir (sepette
    /// IsReturnItem satiri vardi, ProcessExchangeAsync kullanildi).
    /// </summary>
    private async void OnExchangeSaleCompleted(ProcessExchangeResult result)
    {
        // NOT: IsPaymentOpen=false, ClearCart() ve Fark Odeme panelinin
        // acilmasi BURADA YAPILMIYOR - hepsi tek yerde, OnPaymentReadyToClose'da.
        // Fark tutari fis PDF'inden ONCE kaydedilir; PDF uretimi hata verse
        // bile musteriye odenecek fark kaybolmaz.
        // Musteriye kalan fark artik ISLEMIN ICINDE (odeme ekraninda secilen
        // yontemle) iade defterine yaziliyor - sonradan ayri panel acilmaz.
        PendingSettlementAmount = 0;
        // Onceki bilgi mesajinin (orn. "iade aktarildi") altina eklenmez, yerine gecer
        SuccessMessage = result.Message;

        // ── Iade belgesi (bilgi fisi): iade edilen kalemler + iadenin nasil
        // kapatildigi (yeni satista kredi / nakit / kart / hediye karti).
        // Eskiden sadece yeni satis fisi basiliyordu; saf iadede musteriye
        // hicbir belge verilmiyordu.
        try
        {
            var returnedItems = CartItems.Where(i => !i.IsVoided && i.IsReturnItem).ToList();
            if (returnedItems.Count > 0)
            {
                var returnTotal = returnedItems.Sum(i => i.UnitPrice * i.Quantity);
                var payouts = new List<ReceiptPaymentData>();
                if (result.ExchangeCreditApplied > 0)
                    payouts.Add(new ReceiptPaymentData("Yeni satisa mahsup", result.ExchangeCreditApplied));
                if (result.AmountOwedToCustomer > 0)
                    payouts.Add(new ReceiptPaymentData(
                        $"Musteriye iade ({PaymentVM.OwedRefundLabel})", result.AmountOwedToCustomer));

                var returnDoc = new ReceiptData(
                    ReceiptNumber: $"IADE-{DateTime.Now:yyyyMMddHHmmss}",
                    StoreLabel: $"Magaza {_authService.CurrentUser?.StoreId}",
                    CashierName: CashierName,
                    SaleDate: DateTime.Now,
                    Items: returnedItems.Select(i => new ReceiptLineData(
                        $"{i.ProductName} (Fis: {i.SourceReceiptNumber ?? "fissiz"})",
                        i.Barcode, i.Color, i.Size,
                        i.Quantity, i.UnitPrice, 0, i.UnitPrice * i.Quantity)).ToList(),
                    TotalAmount: returnTotal,
                    DiscountAmount: 0,
                    FinalAmount: result.ReturnTotal > 0 ? result.ReturnTotal : returnTotal,
                    Payments: payouts,
                    ChangeAmount: 0,
                    DocumentTitle: "IADE BELGESI",
                    HeaderLines: BuildHeaderLines(includeInvoice: false));
                await _receiptService.GenerateAndSaveReceiptAsync(returnDoc);
            }
        }
        catch (Exception ex)
        {
            SetError($"Iade belgesi PDF kaydedilemedi: {ex.GetType().Name} - {ex.Message}");
        }

        try
        {
            // Fis sadece gercekten yeni bir satis olustuysa basilir (saf
            // iadede NewReceiptNumber null olur, basilacak bir sey yok).
            if (result.NewReceiptNumber is not null)
            {
                var soldItems = CartItems.Where(i => !i.IsVoided && !i.IsReturnItem).ToList();
                var lineSum = soldItems.Sum(i => i.LineTotal);

                // Yeni satis TAM tutariyla kesilir; iade kredisi bir odeme satiri
                // olarak basilir (Ara Toplam - Indirim = Toplam = odemeler).
                var payments = PaymentVM.PaymentLines
                    .Select(p => new ReceiptPaymentData(p.Label, p.Amount))
                    .ToList();
                if (result.ExchangeCreditApplied > 0)
                    payments.Insert(0, new ReceiptPaymentData("Iade Kredisi", result.ExchangeCreditApplied));

                var receiptData = new ReceiptData(
                    ReceiptNumber: result.NewReceiptNumber,
                    StoreLabel: $"Magaza {_authService.CurrentUser?.StoreId}",
                    CashierName: CashierName,
                    SaleDate: DateTime.Now,
                    Items: soldItems.Select(i => new ReceiptLineData(
                        i.ProductName, i.Barcode, i.Color, i.Size,
                        i.Quantity, i.UnitPrice, i.DiscountAmount, i.LineTotal, i.InstallmentSurchargeAmount
                    )).ToList(),
                    TotalAmount: lineSum,
                    DiscountAmount: lineSum - result.NewSaleFinalAmount,
                    FinalAmount: result.NewSaleFinalAmount,
                    Payments: payments,
                    ChangeAmount: PaymentVM.ChangeAmount,
                    HeaderLines: BuildHeaderLines()
                );

                // Fis sessizce kaydedilir - ekstra bir onay/TAMAM popup'i cikmaz.
                await _receiptService.GenerateAndSaveReceiptAsync(receiptData);
            }
        }
        catch (Exception ex)
        {
            SetError($"Fis PDF kaydedilemedi: {ex.GetType().Name} - {ex.Message}");
        }
    }

    /// <summary>Fark odeme paneli: Nakit/Kart/Gider Pusulasi - sadece kayit, API cagrisi yok.</summary>
    [RelayCommand]
    private void ConfirmSettlementNoted(string method)
    {
        AppendSuccessMessage($"Fark ({PendingSettlementAmount:F2} TL) {method} ile karsilandi.");
        IsSettlementPanelOpen = false;
    }

    // Onceki mesaj yoksa basa bos satir eklemeden yazar.
    private void AppendSuccessMessage(string line)
        => SuccessMessage = string.IsNullOrEmpty(SuccessMessage) ? line : $"{SuccessMessage}\n{line}";

    // Indirim girisleri hem "12,5" hem "12.5" kabul eder (makinenin bolge
    // ayarindan bagimsiz) - diger numpad alanlariyla ayni kural.
    private static bool TryParseAmount(string input, out decimal value)
        => decimal.TryParse(input.Replace(",", "."),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out value);

    /// <summary>Fark odeme paneli: Hediye Karti - kart numarasi girilip gercekten yuklenir.</summary>
    [RelayCommand]
    private async Task ConfirmSettlementGiftCardAsync()
    {
        if (string.IsNullOrWhiteSpace(SettlementGiftCardInput))
        {
            SetError("Hediye karti numarasi girin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            await _posService.LoadGiftCardAsync(
                SettlementGiftCardInput.Trim(),
                new LoadGiftCardRequest(PendingSettlementAmount, null));

            AppendSuccessMessage($"Fark ({PendingSettlementAmount:F2} TL) " +
                                 $"'{SettlementGiftCardInput.Trim()}' hediye kartina yuklendi.");
            IsSettlementPanelOpen = false;
        }
        catch (Exception ex)
        {
            SetError($"Hediye kartina yukleme basarisiz: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void DismissSuccessMessage()
        => SuccessMessage = null;

    // Bilgi (yesil) seridi 10 sn sonra kendiliginden kapanir - sepet satirlarini
    // ortmesin. Hata (kirmizi) seridi kasiyer dokunana kadar kalir.
    private int _successVersion;
    partial void OnSuccessMessageChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var version = ++_successVersion;
        _ = ClearSuccessLaterAsync(version);
    }

    private async Task ClearSuccessLaterAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (version == _successVersion) SuccessMessage = null;
    }

    [RelayCommand]
    private void ToggleInstallment4()
    {
        var newValue = SelectedInstallment == 4 ? 0 : 4;
        SetInstallmentForAll(newValue);
        SelectedInstallment = newValue;
        OnPropertyChanged(nameof(Installment4Label));
        OnPropertyChanged(nameof(Installment8Label));
        RecalculateTotals();
    }

    [RelayCommand]
    private void ToggleInstallment8()
    {
        var newValue = SelectedInstallment == 8 ? 0 : 8;
        SetInstallmentForAll(newValue);
        SelectedInstallment = newValue;
        OnPropertyChanged(nameof(Installment4Label));
        OnPropertyChanged(nameof(Installment8Label));
        RecalculateTotals();
    }

    private void SetInstallmentForAll(int installmentCount)
    {
        // Iade satirlari taksit hesabina hic girmez - sadece yeni satis
        // satirlarina uygulanir.
        foreach (var item in CartItems.Where(i => !i.IsVoided && !i.IsReturnItem))
            item.InstallmentCount = installmentCount;
    }

    [RelayCommand]
    private void AppendDigit(string digit)
        => BarcodeInput += digit;

    [RelayCommand]
    private void Backspace()
    {
        if (BarcodeInput.Length > 0)
            BarcodeInput = BarcodeInput[..^1];
    }

    [RelayCommand]
    private void ClearBarcode()
        => BarcodeInput = string.Empty;

    public async Task ScanBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
        var input = BarcodeInput.Trim();
        // Barkod kutusuna urun KODU (W6ER01Z8) yazildiysa: renk/beden secim paneli
        if (ProductCodePattern.IsMatch(input.ToUpperInvariant()))
        {
            BarcodeInput = string.Empty;
            LookupVM.Open(input);
            return;
        }
        await AddProductByBarcodeAsync(input);
        BarcodeInput = string.Empty;
    }

    [RelayCommand]
    private async Task AddByBarcode()
        => await ScanBarcodeAsync();

    public async Task AddProductByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        try
        {
            IsBusy = true;
            ClearError();

            var product = await _productService.GetByBarcodeAsync(barcode);
            if (product is null)
            {
                SetError($"Urun bulunamadi: {barcode}");
                return;
            }

            // Iade satiri ile birlestirme: ayni urunun iadesi + yeniden satisi
            // (degisim) iki ayri satir olmali; aksi halde iade adedi artar.
            var existing = CartItems
                .FirstOrDefault(i => i.VariantId == product.VariantId && !i.IsVoided && !i.IsReturnItem);

            if (existing is not null)
            {
                existing.Quantity++;
            }
            else
            {
                var item = new CartItem
                {
                    VariantId = product.VariantId,
                    Barcode = product.Barcode,
                    ProductName = product.Name,
                    Color = product.Color,
                    Size = product.Size,
                    ImagePath = product.ImagePath,
                    FullImageUrl = _apiClient.ToAbsoluteUrl(product.ImagePath),
                    UnitPrice = product.CurrentPrice,
                    Quantity = 1
                };

                if (SelectedInstallment > 0)
                    item.InstallmentCount = SelectedInstallment;

                CartItems.Add(item);
            }

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            SetError($"Hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveCartItem(CartItem item)
    {
        CartItems.Remove(item);
        RecalculateTotals();
    }

    [RelayCommand]
    private void VoidCartItem(CartItem item)
    {
        if (item is null) return;
        item.IsVoided = !item.IsVoided;
        RecalculateTotals();
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        SelectedCartItem = null;
        CustomerName = null;
        SelectedCustomerId = null;
        ResetDocumentExtras();
        BarcodeInput = string.Empty;
        SelectedInstallment = 0;
        _subDiscountPercent = null;
        SubTotalDiscountAmount = 0;
        SubTotalDiscountLabel = null;
        _discountApprovalToken = null;
        _returnApprovalToken = null;
        _pendingApprovedAction = null;
        OnPropertyChanged(nameof(Installment4Label));
        OnPropertyChanged(nameof(Installment8Label));
        ClearError();
        RecalculateTotals();
    }

    /// <summary>Iade satirina indirim uygulanamaz (fiyat fisten kilitli / backend de yok sayar).</summary>
    private bool RejectDiscountOnReturn(CartItem? item)
    {
        if (item is null)
        {
            SetError("Once sepetten bir satir secin.");
            return true;
        }
        if (item.IsReturnItem)
        {
            SetError("Iade satirina indirim uygulanamaz.");
            return true;
        }
        return false;
    }

    [RelayCommand]
    private void OpenDiscount(CartItem item)
    {
        if (RejectDiscountOnReturn(item)) return;
        if (item is null || item.IsVoided) return;
        SelectedCartItem = item;
        DiscountInput = string.Empty;
        IsDiscountPanelOpen = true;
    }

    [RelayCommand]
    private void ApplyDiscount()
    {
        if (SelectedCartItem is null) return;
        if (!TryParseAmount(DiscountInput, out var value) || value <= 0 || value > 100)
        {
            SetError("Gecersiz deger. 1-100 arasi girin.");
            return;
        }
        var target = SelectedCartItem;
        ClearError();
        RequestDiscountApproval(() =>
        {
            target.DiscountType = DiscountType.Percentage;
            target.DiscountValue = value;
            IsDiscountPanelOpen = false;
            DiscountInput = string.Empty;
            RecalculateTotals();
        });
    }

    [RelayCommand]
    private void CancelDiscount()
    {
        IsDiscountPanelOpen = false;
        DiscountInput = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void OpenManagerDiscount(CartItem item)
    {
        if (RejectDiscountOnReturn(item)) return;
        if (item is null || item.IsVoided) return;
        SelectedCartItem = item;
        ManagerDiscountInput = string.Empty;
        IsManagerDiscountPanelOpen = true;
    }

    [RelayCommand]
    private void ApplyManagerDiscount()
    {
        if (SelectedCartItem is null) return;
        if (!TryParseAmount(ManagerDiscountInput, out var value) || value <= 0)
        {
            SetError("Gecersiz indirim degeri.");
            return;
        }
        var target = SelectedCartItem;
        ClearError();
        RequestDiscountApproval(() =>
        {
            target.DiscountType = DiscountType.Amount;
            target.DiscountValue = value;
            IsManagerDiscountPanelOpen = false;
            ManagerDiscountInput = string.Empty;
            RecalculateTotals();
        });
    }

    // ══════════════════════ YONETICI ONAYI (satir indirimi) ══════════════
    // Backend, satir indirimi iceren her satista gecerli bir "discount" onay
    // token'i ister. Her indirim uygulamasinda yonetici ID + PIN ile onay
    // verir (bir onay baska indirimler icin yeniden kullanilmaz). Token
    // odemeye gecerken suresi dolmussa onay tekrar istenir.
    private const string DiscountApprovalAction = "discount";
    private static readonly TimeSpan ApprovalSafetyMargin = TimeSpan.FromSeconds(30);

    [ObservableProperty] private bool _isManagerApprovalOpen;
    [ObservableProperty] private string _approvalPersonelId = string.Empty;
    [ObservableProperty] private string? _approvalError;

    /// <summary>PasswordBox binding desteklemedigi icin code-behind'dan set edilir.</summary>
    public string ApprovalPin { get; set; } = string.Empty;

    private Action? _pendingApprovedAction;
    private string? _discountApprovalToken;
    private DateTime _discountApprovalExpiresAtUtc;

    private bool HasValidDiscountApproval =>
        _discountApprovalToken is not null
        && DateTime.UtcNow < _discountApprovalExpiresAtUtc - ApprovalSafetyMargin;

    // ── Fissiz iade onayi (ayri token: backend "return-no-receipt" ister) ──
    private const string ReturnNoReceiptApprovalAction = "return-no-receipt";
    private string? _returnApprovalToken;
    private DateTime _returnApprovalExpiresAtUtc;

    private bool HasValidReturnApproval =>
        _returnApprovalToken is not null
        && DateTime.UtcNow < _returnApprovalExpiresAtUtc - ApprovalSafetyMargin;

    /// <summary>Onay penceresi hangi islem icin acik (token hangi alana yazilacak).</summary>
    private string _approvalAction = DiscountApprovalAction;

    [ObservableProperty] private string _approvalReasonText = "Indirim icin Magaza Muduru onayi gereklidir.";

    private void RequestReturnApproval(Action onApproved)
    {
        RequestDiscountApproval(onApproved);
        _approvalAction = ReturnNoReceiptApprovalAction;
        ApprovalReasonText = "Fissiz iade icin Magaza Muduru onayi gereklidir.";
    }

    private void RequestDiscountApproval(Action onApproved)
    {
        _approvalAction = DiscountApprovalAction;
        ApprovalReasonText = "Indirim icin Magaza Muduru onayi gereklidir.";
        _pendingApprovedAction = onApproved;
        ApprovalPersonelId = string.Empty;
        ApprovalPin = string.Empty;
        ApprovalError = null;
        IsManagerApprovalOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmManagerApprovalAsync()
    {
        if (string.IsNullOrWhiteSpace(ApprovalPersonelId) || string.IsNullOrWhiteSpace(ApprovalPin))
        {
            ApprovalError = "Yonetici kodu ve PIN girin.";
            return;
        }

        try
        {
            IsBusy = true;
            ApprovalError = null;

            var response = await _authService.GetApprovalTokenAsync(
                ApprovalPersonelId.Trim(), ApprovalPin, _approvalAction);

            if (_approvalAction == ReturnNoReceiptApprovalAction)
            {
                _returnApprovalToken = response.ApprovalToken;
                _returnApprovalExpiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresInSeconds);
            }
            else
            {
                _discountApprovalToken = response.ApprovalToken;
                _discountApprovalExpiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresInSeconds);
            }

            ApprovalPin = string.Empty;
            IsManagerApprovalOpen = false;

            var action = _pendingApprovedAction;
            _pendingApprovedAction = null;
            action?.Invoke();
        }
        catch (ApiException ex)
        {
            ApprovalError = ex.StatusCode == 403
                ? "Bu kullanicinin onay yetkisi yok (Magaza Muduru/Admin gerekli)."
                : ex.Message;
        }
        catch (Exception ex)
        {
            ApprovalError = $"Baglanti hatasi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelManagerApproval()
    {
        _pendingApprovedAction = null;
        ApprovalPin = string.Empty;
        ApprovalError = null;
        IsManagerApprovalOpen = false;
    }

    [RelayCommand]
    private void CancelManagerDiscount()
    {
        IsManagerDiscountPanelOpen = false;
        ManagerDiscountInput = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void ToggleHamburgerMenu()
        => IsHamburgerMenuOpen = !IsHamburgerMenuOpen;

    [RelayCommand]
    private void CloseHamburgerMenu()
        => IsHamburgerMenuOpen = false;

    [RelayCommand]
    private void VoidLastAction()
    {
        IsHamburgerMenuOpen = false;
        // Secili satir yoksa en son eklenen (iptal edilmemis) satir iptal edilir
        var target = SelectedCartItem ?? CartItems.LastOrDefault(i => !i.IsVoided);
        if (target is null)
        {
            SetError("Iptal edilecek satir yok.");
            return;
        }
        VoidCartItem(target);
    }

    /// <summary>"Fiyat Gor": Urun Bul paneli (barkod alanindaki deger varsa onunla aranir).</summary>
    [RelayCommand]
    private void PriceCheck() => OpenProductLookup();

    [RelayCommand]
    private void DismissError() => ClearError();

    [RelayCommand]
    private void OpenSubTotal()
    {
        if (!CartItems.Any(i => !i.IsVoided))
        {
            SetError("Sepet bos. Urun ekleyin.");
            return;
        }
        ClearError();
        NetTotal = FinalAmount - SubTotalDiscountAmount;
        IsSubTotalOpen = true;
    }

    [RelayCommand]
    private void CloseSubTotal()
        => IsSubTotalOpen = false;

    [RelayCommand]
    private void OpenSubDiscount(string mode)
    {
        SubDiscountInput = string.Empty;
        SubDiscountMode = mode == "percent" ? 1 : 0;
        IsSubDiscountPanelOpen = true;
    }

    [RelayCommand]
    private void ApplySubDiscount()
    {
        if (!decimal.TryParse(
            SubDiscountInput.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) || value <= 0)
        {
            SetError("Gecersiz deger.");
            return;
        }

        // Belge indirimi SADECE yeni satis satirlarina uygulanir (backend:
        // indirim <= yeni satis tutari). Degisimde iade satirlari tabana
        // girerse yuzde indirim eksik hesaplaniyordu.
        var baseAmount = SaleOnlyTotal;
        if (baseAmount <= 0)
        {
            SetError("Yeni satis urunu olmadan belge indirimi uygulanamaz.");
            return;
        }

        if (SubDiscountMode == 1)
        {
            if (value > 100) { SetError("Yuzde 100'den buyuk olamaz."); return; }
            _subDiscountPercent = value;
            SubTotalDiscountAmount = Math.Round(baseAmount * value / 100, 2);
            SubTotalDiscountLabel = $"%{value} Indirim";
        }
        else
        {
            if (value > baseAmount) { SetError($"Indirim yeni satis tutarini ({baseAmount:N2} TL) asamaz."); return; }
            _subDiscountPercent = null;
            SubTotalDiscountAmount = value;
            SubTotalDiscountLabel = $"{value:F2} TL Indirim";
        }

        NetTotal = FinalAmount - SubTotalDiscountAmount;
        IsSubDiscountPanelOpen = false;
        SubDiscountInput = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void CancelSubDiscount()
    {
        IsSubDiscountPanelOpen = false;
        SubDiscountInput = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void RemoveSubDiscount()
    {
        _subDiscountPercent = null;
        SubTotalDiscountAmount = 0;
        SubTotalDiscountLabel = null;
        NetTotal = FinalAmount;
    }

    /// <summary>
    /// Satir indirimi VEYA ara toplam indirimi varsa yonetici onayi gerekir.
    /// (Eskiden sadece satir indirimi onay istiyordu - kasiyer onaysiz %100
    /// ara toplam indirimiyle urunu 0 TL'ye satabiliyordu. Backend de artik
    /// onaysiz belge indirimini reddediyor.)
    /// </summary>
    private bool RequiresDiscountApproval =>
        SubTotalDiscountAmount > 0
        || CartItems.Any(i => !i.IsVoided && !i.IsReturnItem && i.HasDiscount);

    [RelayCommand]
    private void ProceedToPayment()
    {
        // Onay token'i odeme gonderilene kadar gecerli olmali - suresi
        // dolmussa (ya da yoksa) yonetici onayi tekrar istenir.
        if (RequiresDiscountApproval && !HasValidDiscountApproval)
        {
            // Onaydan sonra tekrar buraya donulur - fissiz iade onayi da gerekiyorsa sirada o istenir
            RequestDiscountApproval(ProceedToPayment);
            return;
        }
        if (HasReceiptlessReturn && !HasValidReturnApproval)
        {
            RequestReturnApproval(ProceedToPayment);
            return;
        }
        ProceedToPaymentCore();
    }

    /// <summary>Sepette fissiz (kayitsiz) iade satiri var mi?</summary>
    private bool HasReceiptlessReturn =>
        CartItems.Any(i => !i.IsVoided && i.IsReturnItem && i.SourceReceiptNumber == null);

    private void ProceedToPaymentCore()
    {
        var needsApproval = RequiresDiscountApproval;

        IsSubTotalOpen = false;
        PendingSettlementAmount = 0;

        _paymentViewModel.ResetPaymentState(SelectedInstallment);
        _paymentViewModel.SubTotalDiscount = SubTotalDiscountAmount;
        _paymentViewModel.GrandTotal = FinalAmount;
        _paymentViewModel.CustomerId = SelectedCustomerId;
        _paymentViewModel.ApprovalToken = needsApproval ? _discountApprovalToken : null;
        _paymentViewModel.ReturnApprovalToken = HasReceiptlessReturn ? _returnApprovalToken : null;

        // Sadece yeni satis satirlari (iade satirlari haric) - ProcessSaleAsync/
        // ProcessExchangeAsync'in "NewItems" tarafina gider.
        _paymentViewModel.SaleItems = CartItems
            .Where(i => !i.IsVoided && !i.IsReturnItem)
            .Select(i => new SaleItemRequest(
                i.VariantId,
                i.Quantity,
                i.DiscountType.HasValue ? (int?)i.DiscountType.Value : null,
                i.DiscountValue))
            .ToList();

        // Iade satirlari (varsa) - ProcessExchangeAsync'in "ReturnItems" tarafina gider.
        _paymentViewModel.PendingReturnItems = CartItems
            .Where(i => !i.IsVoided && i.IsReturnItem)
            .Select(i => new ExchangeReturnItemRequest(
                ReceiptNumber: i.SourceReceiptNumber,
                VariantId: i.VariantId,
                RefundQuantity: i.Quantity,
                ReturnReasonId: i.ReturnReasonId,
                ManualUnitPrice: i.SourceReceiptNumber == null ? i.UnitPrice : null))
            .ToList();

        IsPaymentOpen = true;
    }

    [RelayCommand]
    private void GoToPayment()
    {
        if (IsPaymentOpen || IsSubTotalOpen) return;
        if (!CartItems.Any(i => !i.IsVoided))
        {
            SetError("Sepet bos. Urun ekleyin.");
            return;
        }
        OpenSubTotal();
    }

    [RelayCommand]
    private void OpenCustomerSearch()
    {
        _customerSearchViewModel.Reset();
        IsCustomerSearchOpen = true;
    }

    private void RecalculateTotals()
    {
        FinalAmount = CartItems.Where(i => !i.IsVoided).Sum(i => i.LineTotal);
        LineCount = CartItems.Where(i => !i.IsVoided).Sum(i => i.Quantity);

        // Taksit rozetleri (8/4 Taksit Fiyat) SADECE yeni satis satirlarindan
        // hesaplanir - iade satirlari (negatif) bu hesaba hic karismaz.
        var saleOnlyTotal = CartItems
            .Where(i => !i.IsVoided && !i.IsReturnItem)
            .Sum(i => i.LineTotal);
        Installment4 = saleOnlyTotal > 0 ? Math.Round(saleOnlyTotal / 4, 2) : 0;
        Installment8 = saleOnlyTotal > 0 ? Math.Round(saleOnlyTotal / 8, 2) : 0;

        // Ara toplam indirimi sepet degistikce guncel kalmali:
        // yuzde indirim yeni toplamdan yeniden hesaplanir; tutar indirimi
        // yeni toplami asarsa kaldirilir (net tutar eksiye dusmesin).
        if (_subDiscountPercent is decimal pct)
        {
            SubTotalDiscountAmount = saleOnlyTotal > 0 ? Math.Round(saleOnlyTotal * pct / 100, 2) : 0;
            if (SubTotalDiscountAmount == 0) { _subDiscountPercent = null; SubTotalDiscountLabel = null; }
        }
        else if (SubTotalDiscountAmount > 0 && SubTotalDiscountAmount > saleOnlyTotal)
        {
            SubTotalDiscountAmount = 0;
            SubTotalDiscountLabel = null;
            SetError("Ara toplam indirimi yeni toplami astigi icin kaldirildi. Gerekirse yeniden uygulayin.");
        }

        NetTotal = FinalAmount - SubTotalDiscountAmount;

        OnPropertyChanged(nameof(HasPendingExchange));
        OnPropertyChanged(nameof(DocumentModeLabel));
        OnPropertyChanged(nameof(SaleOnlyTotal));
        OnPropertyChanged(nameof(ReturnOnlyTotal));
        OnPropertyChanged(nameof(IsRefundToCustomer));
    }

    /// <summary>Yeni satis satirlarinin toplami (indirim/vade farki dahil).</summary>
    public decimal SaleOnlyTotal => CartItems.Where(i => !i.IsVoided && !i.IsReturnItem).Sum(i => i.LineTotal);

    /// <summary>Iade satirlarinin toplami (pozitif).</summary>
    public decimal ReturnOnlyTotal => -CartItems.Where(i => !i.IsVoided && i.IsReturnItem).Sum(i => i.LineTotal);

    /// <summary>Net tutar eksi: musteriye para iadesi var.</summary>
    public bool IsRefundToCustomer => FinalAmount - SubTotalDiscountAmount < 0;

    /// <summary>Sepette en az bir (iptal edilmemis) iade (Degisim) satiri var mi?</summary>
    public bool HasPendingExchange => CartItems.Any(i => i.IsReturnItem && !i.IsVoided);

    /// <summary>Ust sol etiket: iade satiri varsa "Iade-Degisim", yoksa "Fis".</summary>
    public string DocumentModeLabel => HasPendingExchange
        ? (IsInvoice ? "Iade-Degisim (Fatura)" : "Iade-Degisim")
        : (IsInvoice ? "Fatura" : "Fis");
}