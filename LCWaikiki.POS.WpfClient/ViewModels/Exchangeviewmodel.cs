using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace LCWaikiki.POS.WpfClient.ViewModels;

/// <summary>Bir iade satirinin hangi yontemle bulundugunu belirtir.</summary>
public enum ExchangeReturnSource { Receipt, CustomerPhone, Manual }

public partial class ExchangeReturnItem : ObservableObject
{
    /// <summary>Fisle/Numarayla bulunanlarda dolu; Kayitsiz (Manuel) iadede null.</summary>
    public string? ReceiptNumber { get; init; }
    public int? CustomerId { get; init; }
    public ExchangeReturnSource Source { get; init; }
    public int VariantId { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? Size { get; init; }
    public string? ImagePath { get; init; }
    public string? FullImageUrl { get; init; }

    /// <summary>
    /// True ise bu satir SATIS (yeni urun, ekranin Satis modundayken eklendi,
    /// LineTotal artiya isaretli); false ise IADE (LineTotal eksiye isaretli).
    /// </summary>
    public bool IsSaleItem { get; init; }

    /// <summary>XAML icin: iade satiri mi (iade nedeni butonu sadece iadede gorunur).</summary>
    public bool IsReturnItem => !IsSaleItem;

    /// <summary>Iade satiri ama nedeni secilmemis (gorsel uyari + Satisa Gec engeli).</summary>
    public bool IsMissingReason => !IsSaleItem && ReturnReasonId is null;
    partial void OnReturnReasonIdChanged(int? value) => OnPropertyChanged(nameof(IsMissingReason));

    /// <summary>
    /// Fisle/Numarayla bulunanlarda LineTotal/Quantity'den (kilitli, degistirilemez).
    /// Kayitsiz iadede kasiyerin popup'ta girdigi/degistirdigi manuel fiyat.
    /// </summary>
    public decimal UnitPrice { get; init; }

    /// <summary>
    /// Fisle/Numarayla bulunan iade satirinda iade edilebilecek en fazla adet
    /// (fisteki kalan adet - Fis ekraninda zaten bekleyen iade adedi).
    /// Kayitsiz iade ve satis satirlarinda null (ust sinir yok).
    /// </summary>
    public int? MaxQuantity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(CanIncrease))]
    [NotifyPropertyChangedFor(nameof(CanDecrease))]
    private int _quantity = 1;
    [ObservableProperty] private int? _returnReasonId;
    [ObservableProperty] private string? _returnReasonName;

    /// <summary>Iade ise eksiye, Satis ise artiya isaretli.</summary>
    public decimal LineTotal => IsSaleItem ? UnitPrice * Quantity : -(UnitPrice * Quantity);

    public bool CanIncrease => MaxQuantity is null || Quantity < MaxQuantity;
    public bool CanDecrease => Quantity > 1;

    /// <summary>Satirin kaynagini kasiyere gosteren kisa etiket.</summary>
    public string SourceLabel => IsSaleItem
        ? "SATIS"
        : Source switch
        {
            ExchangeReturnSource.Receipt => $"Fis: {ReceiptNumber}",
            ExchangeReturnSource.CustomerPhone => $"Musteri / Fis: {ReceiptNumber}",
            _ => "Fissiz iade (yonetici onayi gerekir)"
        };
}

/// <summary>
/// Sadece IADE tarafinin arama/sepet-olusturma mantigini icerir. Yeni urun
/// ekleme, odeme ve tamamlama artik bu ViewModel'in isi degil - "Satisa Gec"
/// ile toplanan iade kalemleri gercek MainPosWindow/MainPosViewModel'in
/// sepetine (CartItem, IsReturnItem=true olarak) aktarilir; oradan itibaren
/// normal Fis ekrani akisi (urun ekleme, indirim, taksit, odeme) kullanilir.
/// </summary>
public partial class ExchangeViewModel : BaseViewModel
{
    private readonly PosService _posService;
    private readonly CustomerService _customerService;
    private readonly ProductService _productService;
    private readonly CustomerSearchViewModel _customerSearchViewModel;
    private readonly ApiClient _apiClient;
    private readonly AuthService _authService;
    private readonly CashDrawerService _cashDrawerService;
    private readonly LCWaikiki.POS.WpfClient.Core.ClientSettings _settings;

    // ── Iade sepeti (her 3 yontem de buraya duser) ──
    public ObservableCollection<ExchangeReturnItem> ReturnCart { get; } = new();

    // ── Paylasilan arama alanlari ─────────────────────────────
    [ObservableProperty] private string _receiptNumberInput = string.Empty;
    [ObservableProperty] private string _searchBarcodeInput = string.Empty;

    // ── Coklu Islem: varsayilan KAPALI. Kapaliyken ilk eklenen satirin
    // kaynagina (tek fis ya da tek musteri) kilitlenir. ──
    [ObservableProperty] private bool _isMultiTransactionMode;

    /// <summary>
    /// True iken sag panelden dogrudan taranan/eklenen urunler SATIS (+),
    /// false iken IADE (-) olarak sepete duser. "Iade/Satisa gec" butonu
    /// bu degeri degistirir; asil "devret" islemi ARA TOPLAM/TransferToSaleCommand'da.
    /// </summary>
    [ObservableProperty] private bool _isSaleMode;

    [RelayCommand]
    private void ToggleSaleMode() => IsSaleMode = !IsSaleMode;
    private string? _lockedReceiptNumber;
    private int? _lockedCustomerId;

    // ── Musteri Sec (Numarayla Arama'dan once secilmesi gereken musteri) ──
    public CustomerSearchViewModel CustomerSearchVM => _customerSearchViewModel;

    /// <summary>Urun Bul paneli (urun kodu -> renk -> beden).</summary>
    public ProductLookupViewModel LookupVM { get; }

    [RelayCommand]
    private void OpenProductLookup()
    {
        IsHamburgerMenuOpen = false;
        LookupVM.Open(SearchBarcodeInput.Trim());
        SearchBarcodeInput = string.Empty;
    }
    [ObservableProperty] private bool _isCustomerSearchOpen;
    [ObservableProperty] private CustomerDto? _selectedCustomer;

    public bool HasSelectedCustomer => SelectedCustomer is not null;
    partial void OnSelectedCustomerChanged(CustomerDto? value)
        => OnPropertyChanged(nameof(HasSelectedCustomer));

    // ── Numarayla Arama sonuc listesi ─────────────────────────
    public ObservableCollection<CustomerPurchaseDto> CustomerPurchaseResults { get; } = new();

    // ── Manuel/Kayitsiz Iade popup'i ──────────────────────────
    [ObservableProperty] private bool _isManualPricePanelOpen;
    [ObservableProperty] private string _manualPriceInput = string.Empty;
    [ObservableProperty] private int _manualVariantId;
    [ObservableProperty] private string _manualBarcode = string.Empty;
    [ObservableProperty] private string? _manualProductName;
    [ObservableProperty] private string? _manualColor;
    [ObservableProperty] private string? _manualSize;
    [ObservableProperty] private string? _manualImagePath;
    [ObservableProperty] private string? _manualFullImageUrl;
    private decimal _manualMaxPrice;

    // ── Toplam (sadece bilgi amacli - gercek net hesap artik Fis ekraninda) ──
    [ObservableProperty] private decimal _returnTotal;

    // ── Iade Nedenleri (20 sabit seed) ────────────────────────────
    public ObservableCollection<ReturnReasonDto> ReturnReasons { get; } = new();
    [ObservableProperty] private bool _isReturnReasonPanelOpen;

    // ── Islemler Paneli (yatay hamburger menu, Fis ekranindakiyle ayni gorsel dilde) ──
    [ObservableProperty] private bool _isHamburgerMenuOpen;

    [RelayCommand]
    private void ToggleHamburgerMenu() => IsHamburgerMenuOpen = !IsHamburgerMenuOpen;

    [RelayCommand]
    private void CloseHamburgerMenu() => IsHamburgerMenuOpen = false;

    // ══════════════════════════ FISLE IADE POPUP ══════════════════════
    [ObservableProperty] private bool _isReceiptSearchOpen;
    [ObservableProperty] private SaleReceiptDto? _foundReceiptForPopup;

    /// <summary>XAML icin: popup'ta bir fis yuklu mu (object, bool converter'a dogrudan baglanamaz).</summary>
    public bool HasFoundReceipt => FoundReceiptForPopup is not null;
    partial void OnFoundReceiptForPopupChanged(SaleReceiptDto? value)
        => OnPropertyChanged(nameof(HasFoundReceipt));

    [RelayCommand]
    private void OpenReceiptSearch()
    {
        IsHamburgerMenuOpen = false;
        ReceiptNumberInput = string.Empty;
        FoundReceiptForPopup = null;
        ClearError();
        IsReceiptSearchOpen = true;
    }

    [RelayCommand]
    private void CloseReceiptSearch() => IsReceiptSearchOpen = false;

    /// <summary>"Belge Sec" butonu - fisin TUM icerigini ceker, listede gosterir.</summary>
    [RelayCommand]
    private async Task LoadReceiptForPopupAsync()
    {
        if (string.IsNullOrWhiteSpace(ReceiptNumberInput))
        {
            SetError("Fis numarasi girin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            var receipt = await _posService.GetByReceiptAsync(ReceiptNumberInput.Trim());

            // Tamamen iade edilmis fis de listelenir (kasiyer gecmisi gorsun)
            // ama eklenemeyecegi acikca uyarilir.
            FoundReceiptForPopup = receipt;

            // Fisin musterisi backend'den gelir: henuz musteri secilmediyse otomatik
            // secilir -> degisimin yeni satisi da ayni musteriye yazilir.
            if (receipt.CustomerId is int customerId && SelectedCustomer is null)
            {
                SelectedCustomer = new CustomerDto(customerId, receipt.CustomerPhone ?? string.Empty,
                    receipt.CustomerName ?? $"Musteri #{customerId}", null, false);
                InfoMessage = $"Fisin musterisi secildi: {SelectedCustomer.FullName}";
            }

            if (receipt.Items.Count == 0)
                SetError("Bu fiste urun satiri yok.");
            else if (receipt.Items.All(i => i.RemainingQuantity <= 0))
                SetError("Bu fisteki urunlerin tamami daha once iade edilmis.");
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            SetError($"'{ReceiptNumberInput.Trim()}' numarali fis bulunamadi.");
            FoundReceiptForPopup = null;
        }
        catch (Exception ex)
        {
            SetError($"Fis sorgulanamadi: {ex.Message}");
            FoundReceiptForPopup = null;
        }
        finally { IsBusy = false; }
    }

    /// <summary>Listede bir satira tiklaninca o urunu iade sepetine ekler.</summary>
    [RelayCommand]
    private void AddReceiptItemToCart(SaleItemDto? item)
    {
        if (FoundReceiptForPopup is null || item is null) return;

        if (!IsMultiTransactionMode)
        {
            if (_lockedCustomerId.HasValue)
            {
                SetError("Bu işlem numarayla başlamış. Çoklu İşlem açmadan fişle ürün eklenemez.");
                return;
            }
            if (_lockedReceiptNumber != null && _lockedReceiptNumber != FoundReceiptForPopup.ReceiptNumber)
            {
                SetError($"Bu işlem '{_lockedReceiptNumber}' fişi ile başlamış. " +
                          "Çoklu İşlem açmadan başka fişten ürün eklenemez.");
                return;
            }
        }

        if (!CanAddFromReceipt(FoundReceiptForPopup, item)) return;

        var receiptNumber = FoundReceiptForPopup.ReceiptNumber;
        var remaining = FoundReceiptForPopup.Items
            .Where(i => i.VariantId == item.VariantId)
            .Sum(i => i.RemainingQuantity) - PendingInPosCart(receiptNumber, item.VariantId);

        // Backend'in hesapladigi iade fiyati (ara toplam indirimi/kupon dusulmus)
        var line = AddOrMergeReturnLine(new ExchangeReturnItem
        {
            ReceiptNumber = receiptNumber,
            Source = ExchangeReturnSource.Receipt,
            VariantId = item.VariantId,
            Barcode = item.Barcode,
            ProductName = item.ProductName,
            Color = item.Color,
            Size = item.Size,
            UnitPrice = item.EffectiveRefundUnitPrice,
            MaxQuantity = remaining,
            Quantity = 1
        });

        if (!IsMultiTransactionMode)
            _lockedReceiptNumber = receiptNumber;

        IsReceiptSearchOpen = false;
        ClearError();
        InfoMessage = $"Iade sepetine eklendi: {item.ProductName}";
        PromptReturnReasonIfMissing(line);
    }

    // ══════════════════════════ MUSTERI BILGISIYLE IADE POPUP ═════════
    // NOT: Mahremiyet kurali korunuyor - GetPurchaseHistoryAsync hala
    // barkod zorunlu kiliyor, musterinin TUM gecmisi listelenemez.
    // Bu popup telefon (Musteri Sec ile) + barkod ikisini birden ister.
    [ObservableProperty] private bool _isPhoneSearchOpen;

    [RelayCommand]
    private void OpenPhoneSearch()
    {
        IsHamburgerMenuOpen = false;
        SearchBarcodeInput = string.Empty;
        CustomerPurchaseResults.Clear();
        ClearError();
        IsPhoneSearchOpen = true;
    }

    [RelayCommand]
    private void ClosePhoneSearch() => IsPhoneSearchOpen = false;


    // ── Islemler Paneli komutlari ──
    /// <summary>
    /// "Belge Tipini Degistir": yeni satis belgesi FATURA olarak kesilsin.
    /// Alici bilgisi ARA TOPLAM ile Fis ekranina gecildiginde istenir.
    /// </summary>
    [ObservableProperty] private bool _requestInvoice;

    [RelayCommand]
    private void ChangeDocumentType()
    {
        IsHamburgerMenuOpen = false;
        RequestInvoice = !RequestInvoice;
        InfoMessage = RequestInvoice
            ? "Belge tipi: FATURA - alici bilgisi Fis ekranina gecince istenecek."
            : "Belge tipi: FIS";
    }

    /// <summary>Hediye karti satisi ayri bir belgedir (emanet) - Ana Menu'deki panel acilir.</summary>
    public event Action? GiftCardSaleRequested;

    [RelayCommand]
    private void AddGiftCard()
    {
        IsHamburgerMenuOpen = false;
        GiftCardSaleRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenCashDrawer()
    {
        IsHamburgerMenuOpen = false;
        var error = _cashDrawerService.Open();
        if (error is null) InfoMessage = "Kasa cekmecesi acildi.";
        else SetError(error);
    }

    // ── Hizli Urunler: satis modunda satisa, iade modunda fissiz iadeye eklenir ──
    public IReadOnlyList<LCWaikiki.POS.WpfClient.Core.ClientSettings.QuickProductSetting> QuickProducts
        => _settings.QuickProducts;
    [ObservableProperty] private bool _isQuickProductsOpen;

    [RelayCommand]
    private void ToggleQuickProducts()
    {
        if (QuickProducts.Count == 0)
        {
            SetError("Hizli urun tanimli degil. appsettings.json > QuickProducts listesine Label/Barcode ekleyin.");
            return;
        }
        IsQuickProductsOpen = !IsQuickProductsOpen;
    }

    [RelayCommand]
    private async Task AddQuickProductAsync(LCWaikiki.POS.WpfClient.Core.ClientSettings.QuickProductSetting? product)
    {
        if (product is null) return;
        IsQuickProductsOpen = false;
        SearchBarcodeInput = product.Barcode;
        await ScanBarcodeDirectAsync();
    }

    /// <summary>"Onceki Belge" ekranindan gelindiginde fisi dogrudan Fisle Iade popup'inda acar.</summary>
    public async Task PreloadReceiptAsync(string receiptNumber)
    {
        OpenReceiptSearch();
        ReceiptNumberInput = receiptNumber;
        await LoadReceiptForPopupAsync();
    }

    // ── Kasiyer / bilgi mesaji ─────────────────────────────────
    public string CashierName => _authService.CurrentUser?.FullName ?? "Kasiyer";

    /// <summary>Hata olmayan bilgi mesaji (orn. Fiyat Gor sonucu) - yesil seritte gosterilir.</summary>
    [ObservableProperty] private string _infoMessage = string.Empty;

    /// <summary>"Satir Iptal": sepete en son eklenen satiri kaldirir.</summary>
    [RelayCommand]
    private void CancelLastLine()
    {
        IsHamburgerMenuOpen = false;
        if (ReturnCart.Count == 0)
        {
            SetError("Iptal edilecek satir yok.");
            return;
        }
        var last = ReturnCart[^1];
        ReturnCart.RemoveAt(ReturnCart.Count - 1);
        ClearError();
        InfoMessage = $"Satir iptal edildi: {last.ProductName}";
    }

    /// <summary>"Fiyat Gor": barkod alanindaki urunun guncel fiyatini sepete eklemeden gosterir.</summary>
    [RelayCommand]
    private async Task PriceCheckAsync()
    {
        IsHamburgerMenuOpen = false;
        if (string.IsNullOrWhiteSpace(SearchBarcodeInput))
        {
            SetError("Fiyat gormek icin once barkod girin.");
            return;
        }
        try
        {
            IsBusy = true;
            ClearError();
            var p = await _posService.PriceCheckAsync(SearchBarcodeInput.Trim());
            InfoMessage = $"{p.Name} ({p.Barcode}): {p.CurrentPrice:N2} TL";
        }
        catch (Exception ex)
        {
            SetError($"Fiyat bulunamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void DismissInfo() => InfoMessage = string.Empty;

    // Bilgi seridi birkac saniye sonra kendiliginden kapanir (sepet satirlarinin
    // ustunu kapatmasin); hata seridi ise kasiyer dokunana kadar kalir.
    private int _infoVersion;
    partial void OnInfoMessageChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var version = ++_infoVersion;
        _ = ClearInfoLaterAsync(version);
    }

    private async Task ClearInfoLaterAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (version == _infoVersion) InfoMessage = string.Empty;
    }

    [ObservableProperty] private ExchangeReturnItem? _reasonTargetItem;

    public bool HasNoItems => ReturnCart.Count == 0;

    /// <summary>Sepetteki toplam parca (satir sayisi degil, adetlerin toplami).</summary>
    public int TotalPieceCount => ReturnCart.Sum(i => i.Quantity);

    /// <summary>Iade tutari toplami (pozitif) ve yeni satis toplami - alt bilgi seridi icin.</summary>
    public decimal ReturnOnlyTotal => ReturnCart.Where(i => !i.IsSaleItem).Sum(i => i.UnitPrice * i.Quantity);
    public decimal SaleOnlyTotal => ReturnCart.Where(i => i.IsSaleItem).Sum(i => i.UnitPrice * i.Quantity);

    /// <summary>
    /// Fis ekraninin sepetinde (bu pencere acilmadan once) zaten bekleyen iade
    /// adetleri: ayni fisten ayni urun ikinci kez eklenip kalan adet asilmasin.
    /// Anahtar: (fis no, varyant).
    /// </summary>
    private readonly Dictionary<(string Receipt, int VariantId), int> _pendingInPosCart = new();

    /// <summary>Fis ekranindaki bekleyen iade satirlarini bildirir (pencere acilirken).</summary>
    public void SetPendingReturnsInPosCart(IEnumerable<(string? ReceiptNumber, int VariantId, int Quantity)> pending)
    {
        _pendingInPosCart.Clear();
        foreach (var (receipt, variantId, qty) in pending)
        {
            if (receipt is null) continue; // fissiz iadede ust sinir yok
            var key = (receipt, variantId);
            _pendingInPosCart[key] = _pendingInPosCart.GetValueOrDefault(key) + qty;
        }
    }

    private int PendingInPosCart(string? receiptNumber, int variantId)
        => receiptNumber is null ? 0 : _pendingInPosCart.GetValueOrDefault((receiptNumber, variantId));

    /// <summary>Kasiyer "Satisa Gec"'e basinca, toplanan iade kalemleri ve secili musteriyle tetiklenir.</summary>
    public event Action<List<ExchangeReturnItem>, CustomerDto?>? TransferToSaleRequested;

    /// <summary>Pencere iptal edilince tetiklenir.</summary>
    public event Action? CancelRequested;

    public ExchangeViewModel(
        PosService posService,
        CustomerService customerService,
        ProductService productService,
        CustomerSearchViewModel customerSearchViewModel,
        ApiClient apiClient,
        AuthService authService,
        CashDrawerService cashDrawerService,
        LCWaikiki.POS.WpfClient.Core.ClientSettings settings,
        ProductLookupViewModel lookupViewModel)
    {
        _cashDrawerService = cashDrawerService;
        _settings = settings;
        LookupVM = lookupViewModel;
        LookupVM.AddToCartRequested += async barcode =>
        {
            SearchBarcodeInput = barcode;
            await ScanBarcodeDirectAsync();
        };
        _authService = authService;
        _posService = posService;
        _customerService = customerService;
        _productService = productService;
        _customerSearchViewModel = customerSearchViewModel;
        _apiClient = apiClient;

        ReturnCart.CollectionChanged += (_, e) =>
        {
            // Adet degisince de toplam guncel kalsin
            if (e.NewItems != null)
                foreach (ExchangeReturnItem item in e.NewItems)
                    item.PropertyChanged += (_, pe) =>
                    {
                        if (pe.PropertyName == nameof(ExchangeReturnItem.Quantity))
                            RecalculateReturnTotal();
                    };

            RecalculateReturnTotal();
            OnPropertyChanged(nameof(HasNoItems));
            OnPropertyChanged(nameof(MissingReasonCount));
            _cancelArmed = false;
            if (ReturnCart.Count == 0)
            {
                _lockedReceiptNumber = null;
                _lockedCustomerId = null;
            }
        };

        _customerSearchViewModel.CustomerSelected += customer =>
        {
            SelectedCustomer = customer;
            IsCustomerSearchOpen = false;
        };
        _customerSearchViewModel.CancelRequested += () => IsCustomerSearchOpen = false;
    }

    /// <summary>Pencere her acildiginda cagrilir - onceki oturumun kalintisini temizler.</summary>
    public void Reset()
    {
        ReturnCart.Clear();
        ReceiptNumberInput = string.Empty;
        SearchBarcodeInput = string.Empty;
        IsMultiTransactionMode = false;
        IsSaleMode = false;
        IsCustomerSearchOpen = false;
        IsQuickProductsOpen = false;
        RequestInvoice = false;
        _pendingInPosCart.Clear();
        _lockedReceiptNumber = null;
        _lockedCustomerId = null;
        SelectedCustomer = null;
        CustomerPurchaseResults.Clear();
        IsManualPricePanelOpen = false;
        ManualPriceInput = string.Empty;
        ManualProductName = null;
        ManualColor = null;
        ManualSize = null;
        IsReturnReasonPanelOpen = false;
        ReasonTargetItem = null;
        IsHamburgerMenuOpen = false;
        IsReceiptSearchOpen = false;
        IsPhoneSearchOpen = false;
        FoundReceiptForPopup = null;
        InfoMessage = string.Empty;
        _manualMaxPrice = 0;
        ClearError();
        OnPropertyChanged(nameof(CashierName));
    }

    [RelayCommand]
    private void ToggleMultiTransactionMode()
        => IsMultiTransactionMode = !IsMultiTransactionMode;

    [RelayCommand]
    private void OpenCustomerSearch()
    {
        IsHamburgerMenuOpen = false;
        _customerSearchViewModel.Reset();
        IsCustomerSearchOpen = true;
    }

    // ══════════════════════════ FİŞLE ARAMA ══════════════════════════

    [RelayCommand]
    private async Task SearchReceiptAsync()
    {
        if (string.IsNullOrWhiteSpace(ReceiptNumberInput))
        {
            SetError("Fiş numarası girin.");
            return;
        }
        if (string.IsNullOrWhiteSpace(SearchBarcodeInput))
        {
            SetError("Barkod girin.");
            return;
        }

        var receiptNumber = ReceiptNumberInput.Trim();

        if (!IsMultiTransactionMode)
        {
            if (_lockedCustomerId.HasValue)
            {
                SetError("Bu işlem numarayla başlamış. Çoklu İşlem açmadan fişle ürün eklenemez.");
                return;
            }
            if (_lockedReceiptNumber != null && _lockedReceiptNumber != receiptNumber)
            {
                SetError($"Bu işlem '{_lockedReceiptNumber}' fişi ile başlamış. " +
                          "Çoklu İşlem açmadan başka fişten ürün eklenemez.");
                return;
            }
        }

        try
        {
            IsBusy = true;
            ClearError();

            var receipt = await _posService.GetByReceiptAsync(receiptNumber);
            var barcode = SearchBarcodeInput.Trim();
            var item = receipt.Items.FirstOrDefault(i => i.Barcode == barcode);

            if (item is null)
            {
                SetError($"'{barcode}' barkodlu ürün bu fişte bulunamadı.");
                return;
            }

            if (!CanAddFromReceipt(receipt, item)) return;

            // Backend'in hesapladigi iade fiyati (ara toplam indirimi/kupon dusulmus)
            var unitPrice = item.EffectiveRefundUnitPrice;

            ReturnCart.Add(new ExchangeReturnItem
            {
                ReceiptNumber = receipt.ReceiptNumber,
                Source = ExchangeReturnSource.Receipt,
                VariantId = item.VariantId,
                Barcode = item.Barcode,
                ProductName = item.ProductName,
                Color = item.Color,
                Size = item.Size,
                UnitPrice = unitPrice,
                Quantity = 1
            });

            if (!IsMultiTransactionMode)
                _lockedReceiptNumber = receipt.ReceiptNumber;

            SearchBarcodeInput = string.Empty;
        }
        catch (Exception ex)
        {
            SetError($"Fiş bulunamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════ NUMARAYLA ARAMA ══════════════════════

    [RelayCommand]
    private async Task SearchByCustomerAsync()
    {
        if (SelectedCustomer is null)
        {
            SetError("Önce Müşteri Seç ile müşteri seçin.");
            return;
        }
        if (string.IsNullOrWhiteSpace(SearchBarcodeInput))
        {
            SetError("Barkod girin.");
            return;
        }

        if (!IsMultiTransactionMode)
        {
            if (_lockedReceiptNumber != null)
            {
                SetError("Bu işlem fişle başlamış. Çoklu İşlem açmadan numarayla ürün eklenemez.");
                return;
            }
            if (_lockedCustomerId.HasValue && _lockedCustomerId != SelectedCustomer.Id)
            {
                SetError("Bu işlem farklı bir müşteriyle başlamış. Çoklu İşlem açmadan müşteri değiştirilemez.");
                return;
            }
        }

        try
        {
            IsBusy = true;
            ClearError();

            var results = await _customerService.GetPurchaseHistoryAsync(
                SelectedCustomer.Id, SearchBarcodeInput.Trim());

            CustomerPurchaseResults.Clear();
            foreach (var r in results)
                CustomerPurchaseResults.Add(r);

            if (results.Count == 0)
                SetError("Bu müşteri bu ürünü almamış.");
        }
        catch (Exception ex)
        {
            SetError($"Sorgu hatası: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddCustomerPurchaseToReturn(CustomerPurchaseDto? purchase)
    {
        if (purchase is null || SelectedCustomer is null) return;

        if (!IsMultiTransactionMode)
        {
            if (_lockedReceiptNumber != null)
            {
                SetError("Bu işlem fişle başlamış. Çoklu İşlem açmadan numarayla ürün eklenemez.");
                return;
            }
            if (_lockedCustomerId.HasValue && _lockedCustomerId != SelectedCustomer.Id)
            {
                SetError("Bu işlem farklı bir müşteriyle başlamış. Çoklu İşlem açmadan müşteri değiştirilemez.");
                return;
            }
        }

        // Ayni fiste ayni urun birden fazla satirda olabilir: kalan adet,
        // sonuc listesindeki ayni fis+urun satirlarinin toplamidir.
        var remaining = CustomerPurchaseResults
            .Where(p => p.ReceiptNumber == purchase.ReceiptNumber && p.VariantId == purchase.VariantId)
            .Sum(p => p.Quantity - p.RefundedQuantity);
        if (remaining <= 0) remaining = purchase.Quantity - purchase.RefundedQuantity;
        remaining -= PendingInPosCart(purchase.ReceiptNumber, purchase.VariantId);

        if (remaining <= 0)
        {
            SetError("Bu ürünün tamamı zaten iade edilmiş (ya da Fiş ekranında iade olarak bekliyor).");
            return;
        }
        if (QuantityInCart(purchase.ReceiptNumber, purchase.VariantId) >= remaining)
        {
            SetError($"Bu üründen iade edilebilecek {remaining} adedin hepsi zaten iade sepetinde.");
            return;
        }

        var line = AddOrMergeReturnLine(new ExchangeReturnItem
        {
            ReceiptNumber = purchase.ReceiptNumber,
            CustomerId = SelectedCustomer.Id,
            Source = ExchangeReturnSource.CustomerPhone,
            VariantId = purchase.VariantId,
            Barcode = purchase.Barcode,
            ProductName = purchase.ProductName,
            Color = purchase.Color,
            Size = purchase.Size,
            UnitPrice = purchase.EffectiveRefundUnitPrice,
            MaxQuantity = remaining,
            Quantity = 1
        });

        if (!IsMultiTransactionMode)
            _lockedCustomerId = SelectedCustomer.Id;

        CustomerPurchaseResults.Clear();
        SearchBarcodeInput = string.Empty;
        IsPhoneSearchOpen = false;
        ClearError();
        InfoMessage = $"Iade sepetine eklendi: {purchase.ProductName}";
        PromptReturnReasonIfMissing(line);
    }

    // ══════════════════════════ MANUEL / KAYITSIZ İADE ══════════════════

    [RelayCommand]
    private async Task ScanBarcodeDirectAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchBarcodeInput)) return;

        // Urun KODU (W6ER01Z8) yazildiysa renk/beden secim paneli; secilen
        // varyant moda gore (satis / fissiz iade) eklenir.
        if (MainPosViewModel.ProductCodePattern.IsMatch(SearchBarcodeInput.Trim().ToUpperInvariant()))
        {
            var code = SearchBarcodeInput.Trim();
            SearchBarcodeInput = string.Empty;
            LookupVM.Open(code);
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var product = await _productService.GetByBarcodeAsync(SearchBarcodeInput.Trim());
            if (product is null)
            {
                SetError($"Ürün bulunamadı: {SearchBarcodeInput}");
                return;
            }

            // Satis modunda urun SISTEM FIYATIYLA eklenir - backend yeni satista
            // her zaman kendi aktif fiyatini kullanir, kasiyerin girdigi fiyat
            // yok sayilirdi. Fiyat dusurmek gerekirse Fis ekraninda onayli
            // satir indirimi kullanilir. Fiyat girisi sadece kayitsiz IADE icin.
            if (IsSaleMode)
            {
                AddOrMergeReturnLine(new ExchangeReturnItem
                {
                    ReceiptNumber = null,
                    Source = ExchangeReturnSource.Manual,
                    VariantId = product.VariantId,
                    Barcode = product.Barcode,
                    ProductName = product.Name,
                    Color = product.Color,
                    Size = product.Size,
                    ImagePath = product.ImagePath,
                    FullImageUrl = _apiClient.ToAbsoluteUrl(product.ImagePath),
                    IsSaleItem = true,
                    UnitPrice = product.CurrentPrice,
                    Quantity = 1
                });
                SearchBarcodeInput = string.Empty;
                InfoMessage = product.IsOutOfStock || product.IsNegativeStock
                    ? $"Satisa eklendi: {product.Name} (DIKKAT: magaza stogu {product.StockQuantity})"
                    : $"Satisa eklendi: {product.Name}";
                return;
            }

            if (product.CurrentPrice <= 0)
            {
                SetError($"'{product.Name}' icin aktif fiyat tanimli degil; fissiz iade alinamaz.");
                return;
            }

            ManualVariantId = product.VariantId;
            ManualBarcode = product.Barcode;
            ManualProductName = product.Name;
            ManualColor = product.Color;
            ManualSize = product.Size;
            ManualImagePath = product.ImagePath;
            ManualFullImageUrl = _apiClient.ToAbsoluteUrl(product.ImagePath);
            ManualPriceInput = product.CurrentPrice.ToString("F2", CultureInfo.InvariantCulture);
            _manualMaxPrice = product.CurrentPrice;
            OnPropertyChanged(nameof(ManualMaxPrice));
            IsManualPricePanelOpen = true;
        }
        catch (Exception ex)
        {
            SetError($"Hata: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ConfirmManualPrice()
    {
        var price = ParseDecimal(ManualPriceInput);
        if (price <= 0)
        {
            SetError("Geçerli bir fiyat girin.");
            return;
        }
        // Fişsiz iadede fiyat güncel satış fiyatını aşamaz (backend de reddeder)
        if (_manualMaxPrice > 0 && price > _manualMaxPrice)
        {
            SetError($"Fişsiz iade fiyatı güncel satış fiyatını ({_manualMaxPrice:N2} TL) aşamaz.");
            return;
        }

        var line = AddOrMergeReturnLine(new ExchangeReturnItem
        {
            ReceiptNumber = null,
            Source = ExchangeReturnSource.Manual,
            VariantId = ManualVariantId,
            Barcode = ManualBarcode,
            ProductName = ManualProductName ?? string.Empty,
            Color = ManualColor,
            Size = ManualSize,
            ImagePath = ManualImagePath,
            FullImageUrl = ManualFullImageUrl,
            IsSaleItem = false, // manuel fiyat sadece kayitsiz iadede (satis modu yukarida)
            UnitPrice = Math.Round(price, 2),
            Quantity = 1
        });

        ClearError();
        InfoMessage = $"Fissiz iade eklendi: {line.ProductName} (odemede yonetici onayi istenecek)";
        IsManualPricePanelOpen = false;
        ManualPriceInput = string.Empty;
        ManualProductName = null;
        ManualColor = null;
        ManualSize = null;
        ManualImagePath = null;
        ManualFullImageUrl = null;
        SearchBarcodeInput = string.Empty;
        PromptReturnReasonIfMissing(line);
    }

    /// <summary>Fissiz iade fiyat ust siniri - popup'ta kasiyere gosterilir.</summary>
    public decimal ManualMaxPrice => _manualMaxPrice;

    [RelayCommand]
    private void CancelManualPrice()
    {
        IsManualPricePanelOpen = false;
        ManualPriceInput = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void RemoveReturnItem(ExchangeReturnItem? item)
    {
        if (item is null) return;
        ReturnCart.Remove(item);
        InfoMessage = $"Satir silindi: {item.ProductName}";
    }

    [RelayCommand]
    private void IncreaseQuantity(ExchangeReturnItem? item)
    {
        if (item is null) return;
        if (!item.CanIncrease)
        {
            SetError($"'{item.ProductName}' icin iade edilebilecek en fazla adet {item.MaxQuantity}.");
            return;
        }
        ClearError();
        item.Quantity++;
        OnPropertyChanged(nameof(TotalPieceCount));
    }

    [RelayCommand]
    private void DecreaseQuantity(ExchangeReturnItem? item)
    {
        if (item is null || item.Quantity <= 1) return;
        ClearError();
        item.Quantity--;
        OnPropertyChanged(nameof(TotalPieceCount));
    }

    /// <summary>
    /// Ayni kaynak + ayni urun (+ fissizde ayni fiyat) satiri varsa adedini
    /// artirir, yoksa yeni satir ekler. Eklenen/guncellenen satiri doner.
    /// </summary>
    private ExchangeReturnItem AddOrMergeReturnLine(ExchangeReturnItem candidate)
    {
        var existing = ReturnCart.FirstOrDefault(i =>
            i.IsSaleItem == candidate.IsSaleItem
            && i.Source == candidate.Source
            && i.VariantId == candidate.VariantId
            && i.ReceiptNumber == candidate.ReceiptNumber
            && i.UnitPrice == candidate.UnitPrice);

        if (existing is null)
        {
            ReturnCart.Add(candidate);
            return candidate;
        }

        if (candidate.MaxQuantity is int max)
            existing.MaxQuantity = max;
        existing.Quantity += candidate.Quantity;
        OnPropertyChanged(nameof(TotalPieceCount));
        return existing;
    }

    /// <summary>Iade satirinin nedeni secilmemisse neden popup'ini hemen acar.</summary>
    private void PromptReturnReasonIfMissing(ExchangeReturnItem line)
    {
        if (line.IsMissingReason)
            _ = OpenReturnReasonPanelAsync(line);
    }

    // ══════════════════════════ İADE NEDENİ ══════════════════════════

    public async Task LoadReturnReasonsAsync()
    {
        if (ReturnReasons.Count > 0) return; // zaten yuklendi

        try
        {
            var reasons = await _posService.GetReturnReasonsAsync();
            ReturnReasons.Clear();
            foreach (var r in reasons)
                ReturnReasons.Add(r);
        }
        catch (Exception ex)
        {
            SetError($"İade nedenleri yüklenemedi: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenReturnReasonPanelAsync(ExchangeReturnItem item)
    {
        if (item is null) return;
        await LoadReturnReasonsAsync();
        if (ReturnReasons.Count == 0) return; // hata zaten gosterildi
        ApplyReasonToAll = false;
        ReasonTargetItem = item;
        IsReturnReasonPanelOpen = true;
    }

    [RelayCommand]
    private void SelectReturnReason(ReturnReasonDto reason)
    {
        if (ReasonTargetItem is null || reason is null) return;
        ReasonTargetItem.ReturnReasonId = reason.Id;
        ReasonTargetItem.ReturnReasonName = reason.Name;

        // Kasiyer "tum iade satirlarina uygula" dediyse nedeni secilmemis
        // diger iade satirlarina da ayni neden yazilir.
        if (ApplyReasonToAll)
        {
            foreach (var other in ReturnCart.Where(i => i.IsMissingReason))
            {
                other.ReturnReasonId = reason.Id;
                other.ReturnReasonName = reason.Name;
            }
        }

        IsReturnReasonPanelOpen = false;
        ReasonTargetItem = null;
        OnPropertyChanged(nameof(MissingReasonCount));
        if (MissingReasonCount == 0 && ErrorMessage.Contains("iade nedeni"))
            ClearError();
    }

    [RelayCommand]
    private void CancelReturnReasonPanel()
    {
        IsReturnReasonPanelOpen = false;
        ReasonTargetItem = null;
    }

    /// <summary>Neden popup'inda "nedeni secilmemis tum iade satirlarina uygula".</summary>
    [ObservableProperty] private bool _applyReasonToAll;

    /// <summary>Iade nedeni secilmemis satir sayisi (alt cubukta uyari).</summary>
    public int MissingReasonCount => ReturnCart.Count(i => i.IsMissingReason);

    // ══════════════════════════ SATIŞA GEÇ ══════════════════════════

    [RelayCommand]
    private void TransferToSale()
    {
        if (ReturnCart.Count == 0)
        {
            SetError("Önce iade edilecek ürün ekleyin.");
            return;
        }

        // Her iade satiri icin iade nedeni zorunlu (backend de reddeder)
        var missing = ReturnCart.FirstOrDefault(i => i.IsMissingReason);
        if (missing is not null)
        {
            SetError($"'{missing.ProductName}' için iade nedeni seçin.");
            _ = OpenReturnReasonPanelAsync(missing);
            return;
        }

        TransferToSaleRequested?.Invoke(ReturnCart.ToList(), SelectedCustomer);
    }

    // Sepet doluyken "Geri Don" ilk basista uyarir, ikinci basista cikar
    // (yanlislikla basilip toplanan iade kalemleri kaybolmasin).
    private bool _cancelArmed;

    [RelayCommand]
    private void Cancel()
    {
        CloseAllPopups();
        if (ReturnCart.Count > 0 && !_cancelArmed)
        {
            _cancelArmed = true;
            SetError($"Sepetteki {ReturnCart.Count} satir silinecek. Cikmak icin 'Geri Don'a tekrar basin " +
                     "ya da 'ARA TOPLAM' ile Fis ekranina aktarin.");
            return;
        }
        _cancelArmed = false;
        CancelRequested?.Invoke();
    }

    /// <summary>
    /// Esc / Geri: en ustteki popup'i kapatir. Kapatilacak popup varsa true doner.
    /// </summary>
    public bool CloseTopmostPopup()
    {
        if (LookupVM.IsOpen) { LookupVM.IsOpen = false; return true; }
        if (IsReturnReasonPanelOpen) { CancelReturnReasonPanel(); return true; }
        if (IsManualPricePanelOpen) { CancelManualPrice(); return true; }
        if (IsCustomerSearchOpen) { IsCustomerSearchOpen = false; return true; }
        if (IsReceiptSearchOpen) { IsReceiptSearchOpen = false; return true; }
        if (IsPhoneSearchOpen) { IsPhoneSearchOpen = false; return true; }
        if (IsQuickProductsOpen) { IsQuickProductsOpen = false; return true; }
        if (IsHamburgerMenuOpen) { IsHamburgerMenuOpen = false; return true; }
        return false;
    }

    private void CloseAllPopups()
    {
        while (CloseTopmostPopup()) { }
    }

    [RelayCommand]
    private void DismissError() => ClearError();

    /// <summary>
    /// Fisteki urun iade sepetine eklenebilir mi? Daha once iade edilmis
    /// adetler (RefundedQuantity) ve sepette bekleyenler dusulur. Ayni urun
    /// fiste birden fazla satirdaysa toplam kalan adede bakilir.
    /// </summary>
    private bool CanAddFromReceipt(SaleReceiptDto receipt, SaleItemDto item)
    {
        var receiptNumber = receipt.ReceiptNumber;
        var remaining = receipt.Items.Where(i => i.VariantId == item.VariantId).Sum(i => i.RemainingQuantity);

        if (remaining <= 0)
        {
            SetError($"'{item.ProductName}' bu fişten daha önce tamamen iade edilmiş.");
            return false;
        }

        var pending = PendingInPosCart(receiptNumber, item.VariantId);
        if (pending > 0)
        {
            remaining -= pending;
            if (remaining <= 0)
            {
                SetError($"'{item.ProductName}' bu fişten zaten Fiş ekranında iade olarak bekliyor.");
                return false;
            }
        }
        if (QuantityInCart(receiptNumber, item.VariantId) >= remaining)
        {
            SetError($"Bu fişte '{item.ProductName}' için iade edilebilecek {remaining} adedin hepsi zaten iade sepetinde.");
            return false;
        }
        return true;
    }

    /// <summary>Ayni fis + urun icin iade sepetinde zaten bekleyen adet.</summary>
    private int QuantityInCart(string? receiptNumber, int variantId)
        => ReturnCart
            .Where(i => !i.IsSaleItem && i.ReceiptNumber == receiptNumber && i.VariantId == variantId)
            .Sum(i => i.Quantity);

    private void RecalculateReturnTotal()
    {
        ReturnTotal = ReturnCart.Sum(i => i.LineTotal);
        OnPropertyChanged(nameof(TotalPieceCount));
        OnPropertyChanged(nameof(ReturnOnlyTotal));
        OnPropertyChanged(nameof(SaleOnlyTotal));
    }

    private static decimal ParseDecimal(string input)
    {
        var normalized = input.Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}