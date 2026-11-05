using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace LCWaikiki.POS.WpfClient.ViewModels;

/// <summary>
/// Fis ekraninin Islemler menusu ve alt cubuk araclari: Askiya Al/Cagir,
/// Belge Tipi (Fis/Fatura), Ulke, Satis Danismani (Personel), Onceki Belge,
/// Cekmece Ac, Hizli Urunler, Hediye Karti satisi.
/// </summary>
public partial class MainPosViewModel
{
    private readonly SuspendedCartService _suspendedCartService;
    private readonly CashDrawerService _cashDrawerService;
    private readonly ClientSettings _settings;

    /// <summary>Hediye karti satisi ayri belgedir: Ana Menu'deki H.Kart Satis paneli acilir.</summary>
    public event Action? OpenGiftCardSaleRequested;

    /// <summary>"Onceki Belge" panelinden iade/degisim baslatilir (fis no ile).</summary>
    public event Action<string>? OpenExchangeWithReceiptRequested;

    /// <summary>Herhangi bir arac paneli acik mi (Esc ile kapatma icin).</summary>
    public bool CloseTopmostToolPanel()
    {
        if (LookupVM.IsOpen) { LookupVM.IsOpen = false; return true; }
        if (IsInvoicePanelOpen) { CancelInvoice(); return true; }
        if (IsCountryPanelOpen) { IsCountryPanelOpen = false; return true; }
        if (IsSalesPersonPanelOpen) { IsSalesPersonPanelOpen = false; return true; }
        if (IsSuspendedListOpen) { IsSuspendedListOpen = false; return true; }
        if (IsLastReceiptOpen) { IsLastReceiptOpen = false; return true; }
        if (IsQuickProductsOpen) { IsQuickProductsOpen = false; return true; }
        if (IsHamburgerMenuOpen) { IsHamburgerMenuOpen = false; return true; }
        return false;
    }

    private bool RejectWhenNotOnFis()
    {
        IsHamburgerMenuOpen = false;
        if (!IsPaymentOpen && !IsSubTotalOpen) return false;
        SetError("Bu islem icin once Fis ekranina geri donun.");
        return true;
    }

    // ══════════════════════ BELGE TIPI: FIS / FATURA ══════════════════════
    // Backend'de fatura alanlari olmadigi icin alici bilgisi belgeye (PDF)
    // basilir; satis kaydinda sadece musteri (CustomerId) tutulur.

    [ObservableProperty] private bool _isInvoice;
    [ObservableProperty] private bool _isInvoicePanelOpen;
    [ObservableProperty] private string _invoiceBuyerName = string.Empty;
    [ObservableProperty] private string _invoiceTaxId = string.Empty;
    [ObservableProperty] private string _invoiceTaxOffice = string.Empty;
    [ObservableProperty] private string _invoiceAddress = string.Empty;
    [ObservableProperty] private string? _invoiceError;

    partial void OnIsInvoiceChanged(bool value) => OnPropertyChanged(nameof(DocumentModeLabel));

    /// <summary>"Fatura ile Devam Et": belge tipini faturaya cevirip alici bilgisini ister.</summary>
    [RelayCommand]
    private void ContinueWithInvoice()
    {
        if (RejectWhenNotOnFis()) return;
        if (string.IsNullOrWhiteSpace(InvoiceBuyerName) && CustomerName is not null)
            InvoiceBuyerName = CustomerName;
        InvoiceError = null;
        IsInvoicePanelOpen = true;
    }

    /// <summary>"Belge Tipini Degistir": Fis &lt;-&gt; Fatura.</summary>
    [RelayCommand]
    private void ChangeDocumentType()
    {
        if (RejectWhenNotOnFis()) return;
        if (IsInvoice)
        {
            IsInvoice = false;
            SuccessMessage = "Belge tipi: FIS";
            return;
        }
        ContinueWithInvoice();
    }

    [RelayCommand]
    private void ConfirmInvoice()
    {
        var name = InvoiceBuyerName.Trim();
        var taxId = new string(InvoiceTaxId.Where(char.IsDigit).ToArray());
        if (name.Length < 3)
        {
            InvoiceError = "Alici adi / unvani girin.";
            return;
        }
        if (taxId.Length == 11)
        {
            if (!IsValidTckn(taxId)) { InvoiceError = "TC Kimlik No gecersiz."; return; }
        }
        else if (taxId.Length != 10)
        {
            InvoiceError = "Vergi No 10 hane, TC Kimlik No 11 hane olmalidir.";
            return;
        }
        if (taxId.Length == 10 && string.IsNullOrWhiteSpace(InvoiceTaxOffice))
        {
            InvoiceError = "Kurumsal fatura icin vergi dairesi girin.";
            return;
        }
        if (InvoiceAddress.Trim().Length < 5)
        {
            InvoiceError = "Fatura adresi girin.";
            return;
        }

        InvoiceTaxId = taxId;
        InvoiceError = null;
        IsInvoice = true;
        IsInvoicePanelOpen = false;
        SuccessMessage = $"Belge tipi: FATURA ({name})";
    }

    [RelayCommand]
    private void CancelInvoice()
    {
        IsInvoicePanelOpen = false;
        InvoiceError = null;
    }

    /// <summary>TCKN algoritmasi: 10. hane = ((tekler*7 - ciftler) mod 10), 11. hane = ilk 10 toplami mod 10.</summary>
    internal static bool IsValidTckn(string t)
    {
        if (t.Length != 11 || t[0] == '0' || !t.All(char.IsDigit)) return false;
        var d = t.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var d10 = ((odd * 7 - even) % 10 + 10) % 10;
        var d11 = d.Take(10).Sum() % 10;
        return d[9] == d10 && d[10] == d11;
    }

    // ══════════════════════ ULKE ══════════════════════

    public IReadOnlyList<string> Countries => _settings.Countries;
    [ObservableProperty] private string _customerCountry = "Turkiye";
    [ObservableProperty] private bool _isCountryPanelOpen;

    public string CountryButtonLabel => IsForeignCustomer ? $"Ulke: {CustomerCountry}" : "Ulke";
    public bool IsForeignCustomer => CustomerCountry != (_settings.Countries.FirstOrDefault() ?? "Turkiye");
    partial void OnCustomerCountryChanged(string value)
    {
        OnPropertyChanged(nameof(CountryButtonLabel));
        OnPropertyChanged(nameof(IsForeignCustomer));
    }

    [RelayCommand]
    private void OpenCountryPanel()
    {
        if (RejectWhenNotOnFis()) return;
        IsCountryPanelOpen = true;
    }

    [RelayCommand]
    private void CloseCountryPanel() => IsCountryPanelOpen = false;

    [RelayCommand]
    private void SelectCountry(string country)
    {
        if (string.IsNullOrWhiteSpace(country)) return;
        CustomerCountry = country;
        IsCountryPanelOpen = false;
        SuccessMessage = IsForeignCustomer
            ? $"Musteri ulkesi: {country} (belgeye basilir)"
            : "Musteri ulkesi: Turkiye";
    }

    // ══════════════════════ PERSONEL (SATIS DANISMANI) ══════════════════════

    [ObservableProperty] private string? _salesPersonId;
    [ObservableProperty] private string _salesPersonInput = string.Empty;
    [ObservableProperty] private bool _isSalesPersonPanelOpen;

    public string SalesPersonButtonLabel => string.IsNullOrEmpty(SalesPersonId)
        ? "Personel Arama" : $"Personel: {SalesPersonId}";
    partial void OnSalesPersonIdChanged(string? value) => OnPropertyChanged(nameof(SalesPersonButtonLabel));

    [RelayCommand]
    private void OpenSalesPersonPanel()
    {
        if (RejectWhenNotOnFis()) return;
        SalesPersonInput = SalesPersonId ?? string.Empty;
        IsSalesPersonPanelOpen = true;
    }

    [RelayCommand]
    private void ConfirmSalesPerson()
    {
        var id = SalesPersonInput.Trim();
        if (id.Length == 0)
        {
            SalesPersonId = null;
            IsSalesPersonPanelOpen = false;
            SuccessMessage = "Satis danismani kaldirildi.";
            return;
        }
        if (id.Length < 4 || id.Length > 10 || !id.All(char.IsDigit))
        {
            SetError("Personel numarasi 4-10 haneli rakam olmalidir.");
            return;
        }
        ClearError();
        SalesPersonId = id;
        IsSalesPersonPanelOpen = false;
        SuccessMessage = $"Satis danismani: {id} (belgeye basilir)";
    }

    [RelayCommand]
    private void CancelSalesPerson() => IsSalesPersonPanelOpen = false;

    // ── Belge ust bilgisi (PDF) ──
    private List<string> BuildHeaderLines(bool includeInvoice = true)
    {
        var lines = new List<string>();
        if (includeInvoice && IsInvoice)
        {
            lines.Add("BELGE TIPI: E-ARSIV FATURA");
            lines.Add($"Alici: {InvoiceBuyerName.Trim()}");
            lines.Add(InvoiceTaxId.Length == 11 ? $"TCKN: {InvoiceTaxId}" : $"VKN: {InvoiceTaxId} / VD: {InvoiceTaxOffice.Trim()}");
            lines.Add($"Adres: {InvoiceAddress.Trim()}");
        }
        if (CustomerName is not null) lines.Add($"Musteri: {CustomerName}");
        if (IsForeignCustomer) lines.Add($"Musteri Ulkesi: {CustomerCountry}");
        if (!string.IsNullOrEmpty(SalesPersonId)) lines.Add($"Satis Danismani: {SalesPersonId}");
        return lines;
    }

    private void ResetDocumentExtras()
    {
        IsInvoice = false;
        IsInvoicePanelOpen = false;
        InvoiceBuyerName = InvoiceTaxId = InvoiceTaxOffice = InvoiceAddress = string.Empty;
        InvoiceError = null;
        CustomerCountry = _settings.Countries.FirstOrDefault() ?? "Turkiye";
        SalesPersonId = null;
        IsCountryPanelOpen = IsSalesPersonPanelOpen = IsSuspendedListOpen = IsLastReceiptOpen = IsQuickProductsOpen = false;
    }

    // ══════════════════════ ASKIYA AL / ASKIDAN CAGIR ══════════════════════

    public ObservableCollection<SuspendedCartDto> SuspendedCarts { get; } = new();
    [ObservableProperty] private bool _isSuspendedListOpen;

    /// <summary>Sepet doluysa askiya alir; bossa askidaki sepetleri listeler.</summary>
    [RelayCommand]
    private async Task SuspendOrResumeAsync()
    {
        if (RejectWhenNotOnFis()) return;
        if (CartItems.Any(i => !i.IsVoided))
            await SuspendCurrentCartAsync();
        else
            await OpenSuspendedListAsync();
    }

    private async Task SuspendCurrentCartAsync()
    {
        var data = new SuspendedCartData
        {
            CustomerId = SelectedCustomerId,
            CustomerName = CustomerName,
            InstallmentCount = SelectedInstallment,
            SalesPersonId = SalesPersonId,
            Country = CustomerCountry,
            Lines = CartItems.Where(i => !i.IsVoided).Select(i => new SuspendedCartLine
            {
                VariantId = i.VariantId,
                Barcode = i.Barcode,
                ProductName = i.ProductName,
                Color = i.Color,
                Size = i.Size,
                ImagePath = i.ImagePath,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                IsReturnItem = i.IsReturnItem,
                SourceReceiptNumber = i.SourceReceiptNumber,
                ReturnReasonId = i.ReturnReasonId,
                ReturnReasonName = i.ReturnReasonName,
                DiscountType = i.DiscountTypeValue,
                DiscountValue = i.DiscountValue
            }).ToList()
        };

        try
        {
            IsBusy = true;
            ClearError();
            var pieces = data.Lines.Sum(l => l.Quantity);
            var note = $"{CustomerName ?? "Musterisiz"} - {pieces} parca - {FinalAmount:N2} TL";
            var result = await _suspendedCartService.SuspendAsync(JsonSerializer.Serialize(data), note);
            ClearCart();
            SuccessMessage = $"Sepet askiya alindi (No: {result.Id}). Geri cagirmak icin sepet bosken 'Askiya Al'a basin.";
        }
        catch (Exception ex)
        {
            SetError($"Sepet askiya alinamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task OpenSuspendedListAsync()
    {
        try
        {
            IsBusy = true;
            ClearError();
            var list = await _suspendedCartService.GetAllAsync();
            SuspendedCarts.Clear();
            foreach (var c in list) SuspendedCarts.Add(c);
            if (list.Count == 0)
            {
                SuccessMessage = "Askida bekleyen sepet yok.";
                return;
            }
            IsSuspendedListOpen = true;
        }
        catch (Exception ex)
        {
            SetError($"Askidaki sepetler alinamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResumeSuspendedAsync(SuspendedCartDto? cart)
    {
        if (cart is null) return;
        if (CartItems.Any(i => !i.IsVoided))
        {
            SetError("Askidaki sepeti cagirmak icin once mevcut sepeti bitirin ya da askiya alin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            var result = await _suspendedCartService.ResumeAsync(cart.Id);
            var data = JsonSerializer.Deserialize<SuspendedCartData>(result.CartDataJson)
                       ?? throw new InvalidOperationException("Sepet verisi okunamadi.");

            ClearCart();
            SelectedCustomerId = data.CustomerId;
            CustomerName = data.CustomerName;
            SalesPersonId = data.SalesPersonId;
            if (!string.IsNullOrEmpty(data.Country)) CustomerCountry = data.Country;
            foreach (var l in data.Lines)
            {
                CartItems.Add(new CartItem
                {
                    VariantId = l.VariantId,
                    Barcode = l.Barcode,
                    ProductName = l.ProductName,
                    Color = l.Color,
                    Size = l.Size,
                    ImagePath = l.ImagePath,
                    FullImageUrl = _apiClient.ToAbsoluteUrl(l.ImagePath),
                    UnitPrice = l.UnitPrice,
                    Quantity = l.Quantity,
                    IsReturnItem = l.IsReturnItem,
                    SourceReceiptNumber = l.SourceReceiptNumber,
                    ReturnReasonId = l.ReturnReasonId,
                    ReturnReasonName = l.ReturnReasonName,
                    DiscountTypeValue = l.IsReturnItem ? null : l.DiscountType,
                    DiscountValue = l.IsReturnItem ? null : l.DiscountValue
                });
            }
            if (data.InstallmentCount is 4 or 8)
            {
                SelectedInstallment = data.InstallmentCount;
                SetInstallmentForAll(data.InstallmentCount);
                OnPropertyChanged(nameof(Installment4Label));
                OnPropertyChanged(nameof(Installment8Label));
            }
            RecalculateTotals();
            IsSuspendedListOpen = false;
            SuccessMessage = $"Askidaki sepet geri cagrildi (No: {cart.Id}). Fiyat/indirimler odemede yeniden dogrulanir.";
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            SetError("Bu sepet baska bir kasada cagrilmis ya da silinmis.");
            SuspendedCarts.Remove(cart);
        }
        catch (Exception ex)
        {
            SetError($"Sepet geri cagrilamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CloseSuspendedList() => IsSuspendedListOpen = false;

    // ══════════════════════ ONCEKI BELGEYE DONUS ══════════════════════

    [ObservableProperty] private bool _isLastReceiptOpen;
    [ObservableProperty] private SaleReceiptDto? _lastReceipt;

    /// <summary>Kasiyerin son tamamlanan belgesini gosterir; buradan iade/degisim baslatilabilir.</summary>
    [RelayCommand]
    private async Task OpenLastReceiptAsync()
    {
        if (RejectWhenNotOnFis()) return;
        try
        {
            IsBusy = true;
            ClearError();
            var last = await _posService.GetLastSaleAsync();
            LastReceipt = await _posService.GetByReceiptAsync(last.ReceiptNumber);
            IsLastReceiptOpen = true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            SetError("Bu kasiyerin tamamlanmis bir belgesi yok.");
        }
        catch (Exception ex)
        {
            SetError($"Onceki belge alinamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ExchangeFromLastReceipt()
    {
        if (LastReceipt is null) return;
        var receipt = LastReceipt.ReceiptNumber;
        IsLastReceiptOpen = false;
        OpenExchangeWithReceiptRequested?.Invoke(receipt);
    }

    [RelayCommand]
    private void CloseLastReceipt() => IsLastReceiptOpen = false;

    // ══════════════════════ CEKMECE / HEDIYE KARTI ══════════════════════

    [RelayCommand]
    private void OpenCashDrawer()
    {
        IsHamburgerMenuOpen = false;
        var error = _cashDrawerService.Open();
        if (error is null) SuccessMessage = "Kasa cekmecesi acildi.";
        else SetError(error);
    }

    [RelayCommand]
    private void OpenGiftCardSale()
    {
        if (RejectWhenNotOnFis()) return;
        OpenGiftCardSaleRequested?.Invoke();
    }

    // ══════════════════════ HIZLI URUNLER ══════════════════════

    public IReadOnlyList<ClientSettings.QuickProductSetting> QuickProducts => _settings.QuickProducts;
    [ObservableProperty] private bool _isQuickProductsOpen;

    [RelayCommand]
    private void ToggleQuickProducts()
    {
        if (RejectWhenNotOnFis()) return;
        if (QuickProducts.Count == 0)
        {
            SetError("Hizli urun tanimli degil. appsettings.json > QuickProducts listesine Label/Barcode ekleyin.");
            return;
        }
        IsQuickProductsOpen = !IsQuickProductsOpen;
    }

    [RelayCommand]
    private async Task AddQuickProductAsync(ClientSettings.QuickProductSetting? product)
    {
        if (product is null) return;
        IsQuickProductsOpen = false;
        await AddProductByBarcodeAsync(product.Barcode);
        if (string.IsNullOrEmpty(ErrorMessage))
            SuccessMessage = $"Eklendi: {product.Label}";
    }

    // ══════════════════════ UST SEKMELER ══════════════════════

    /// <summary>"Urun Ekle/Cikar" sekmesi: Ara Toplam'dan Fis ekranina doner.</summary>
    [RelayCommand]
    private void GoToProductsTab()
    {
        if (IsPaymentOpen) { SetError("Odeme ekranindan 'Geri Don' ile cikin."); return; }
        IsSubTotalOpen = false;
        CloseTopmostToolPanel();
    }

    /// <summary>"Kampanya/Indirim" sekmesi: Ara Toplam (belge indirimi) ekranini acar.</summary>
    [RelayCommand]
    private void GoToDiscountTab()
    {
        if (IsPaymentOpen) return;
        OpenSubTotal();
    }
}

public partial class MainPosViewModel
{
    /// <summary>Iade-Degisim ekranindan hediye karti satisi istendiginde (Fis kontrolu olmadan).</summary>
    public void RaiseGiftCardSaleRequested() => OpenGiftCardSaleRequested?.Invoke();
}

public partial class MainPosViewModel
{
    /// <summary>
    /// Istisnai Satis: liste fiyati disinda (hasarli urun, personel vb.) satis.
    /// Fiyat dusurme yonetici onayli satir indirimiyle yapilir; backend onaysiz
    /// indirimi reddeder.
    /// </summary>
    public void ShowExceptionalSaleHint()
        => SuccessMessage = "ISTISNAI SATIS: urunu okutun, satiri secip 'Tutar Indirim' ya da 'Yuzde Indirim' " +
                            "ile ozel fiyati girin. Odemeye geciste Magaza Muduru onayi istenir.";
}
