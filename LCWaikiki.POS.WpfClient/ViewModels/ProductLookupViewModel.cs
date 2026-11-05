using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace LCWaikiki.POS.WpfClient.ViewModels;

/// <summary>Renk secenegi (secili durumu ekranda vurgulanir).</summary>
public partial class LookupColorOption : ObservableObject
{
    public required ProductColorOptionDto Dto { get; init; }
    public string Color => Dto.Color;
    public int TotalStock => Dto.TotalStock;
    public string? FullImageUrl { get; init; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Beden secenegi.</summary>
public partial class LookupSizeOption : ObservableObject
{
    public required ProductSizeOptionDto Dto { get; init; }
    public string Size => Dto.Size ?? "Std";
    public int Stock => Dto.StockQuantity;
    public bool IsSellable => Dto.IsSellable;
    public string StockText => Dto.StockQuantity > 0 ? $"{Dto.StockQuantity} ad" : "stok yok";
    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// Barkodsuz urun bulma / Fiyat Gor:
///   urun kodu (W6ER01Z8) | barkod | kod parcasi / ad
///   -> urun -> RENK secimi -> BEDEN secimi -> fiyat + magaza stogu -> (Sepete Ekle)
/// Fis ekrani ve Ana Menu ayni ViewModel'i kullanir.
/// </summary>
public partial class ProductLookupViewModel : BaseViewModel
{
    private static readonly Regex CodePattern = new("^[WS][0-9][A-Z]{2}[0-9A-Z]{2}Z[148]$", RegexOptions.Compiled);

    private readonly ProductService _productService;
    private readonly ApiClient _apiClient;

    public ProductLookupViewModel(ProductService productService, ApiClient apiClient)
    {
        _productService = productService;
        _apiClient = apiClient;
    }

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ProductByCodeDto? _product;
    [ObservableProperty] private string? _productImageUrl;
    [ObservableProperty] private LookupColorOption? _selectedColor;
    [ObservableProperty] private LookupSizeOption? _selectedSize;

    /// <summary>"Sepete Ekle" butonu gorunsun mu (Fis ekraninda / Ana Menu'den).</summary>
    [ObservableProperty] private bool _canAddToCart = true;

    public ObservableCollection<ProductSearchItemDto> SearchResults { get; } = new();
    public ObservableCollection<LookupColorOption> Colors { get; } = new();
    public ObservableCollection<LookupSizeOption> Sizes { get; } = new();

    public bool HasProduct => Product is not null;
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasSelection => SelectedSize is not null;

    /// <summary>Kod parcalarinin aciklamasi: "Kis 2026 · Erkek · Yetiskin (Z8) · Erkek Giyim".</summary>
    public string ClassificationText => Product is null ? string.Empty
        : string.Join("  ·  ", new[] { Product.SeasonLabel, Product.DepartmentLabel, Product.AgeGroupLabel, Product.CategoryName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PriceRangeText => Product is null ? string.Empty
        : Product.MinPrice == Product.MaxPrice ? $"{Product.MinPrice:N2} TL" : $"{Product.MinPrice:N2} - {Product.MaxPrice:N2} TL";

    /// <summary>Sepete eklenecek varyantin barkodu (host ekleme isini yapar).</summary>
    public event Action<string>? AddToCartRequested;

    partial void OnProductChanged(ProductByCodeDto? value)
    {
        OnPropertyChanged(nameof(HasProduct));
        OnPropertyChanged(nameof(ClassificationText));
        OnPropertyChanged(nameof(PriceRangeText));
    }

    partial void OnSelectedSizeChanged(LookupSizeOption? value) => OnPropertyChanged(nameof(HasSelection));

    public void Open(string? initialQuery = null, bool canAddToCart = true)
    {
        Reset();
        CanAddToCart = canAddToCart;
        IsOpen = true;
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            Query = initialQuery.Trim();
            _ = SearchAsync();
        }
    }

    private void Reset()
    {
        Query = string.Empty;
        Product = null;
        ProductImageUrl = null;
        SearchResults.Clear();
        Colors.Clear();
        Sizes.Clear();
        SelectedColor = null;
        SelectedSize = null;
        OnPropertyChanged(nameof(HasSearchResults));
        ClearError();
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>Sorgu: 8 karakterlik kod -> dogrudan urun; tamamen rakam -> barkod; diger -> arama.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        var q = Query.Trim();
        if (q.Length < 2)
        {
            SetError("Urun kodu (orn. W6ER01Z8), barkod ya da en az 2 harf ad girin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            SearchResults.Clear();
            OnPropertyChanged(nameof(HasSearchResults));
            var upper = q.ToUpperInvariant();

            if (CodePattern.IsMatch(upper))
            {
                await LoadProductAsync(upper, preselectBarcode: null);
                return;
            }

            if (q.All(char.IsDigit) && q.Length >= 8)
            {
                // Barkod: varyanti bul, urun kodundan tum renk/bedenleri getir, o varyanti sec
                var variant = await _productService.GetByBarcodeAsync(q);
                if (!string.IsNullOrEmpty(variant.ProductCode))
                    await LoadProductAsync(variant.ProductCode, preselectBarcode: variant.Barcode);
                else
                    SetError($"{variant.Name}: {variant.CurrentPrice:N2} TL (urun kodu tanimsiz).");
                return;
            }

            var results = await _productService.SearchAsync(q);
            Product = null;
            Colors.Clear();
            Sizes.Clear();
            foreach (var r in results) SearchResults.Add(r);
            OnPropertyChanged(nameof(HasSearchResults));
            if (results.Count == 0)
                SetError($"'{q}' ile eslesen urun yok.");
            else if (results.Count == 1)
                await LoadProductAsync(results[0].Code, null);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            SetError(ex.Message);
            Product = null;
        }
        catch (Exception ex)
        {
            SetError($"Urun bulunamadi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PickSearchResultAsync(ProductSearchItemDto? item)
    {
        if (item is null) return;
        try
        {
            IsBusy = true;
            ClearError();
            await LoadProductAsync(item.Code, null);
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task LoadProductAsync(string code, string? preselectBarcode)
    {
        var product = await _productService.GetByCodeAsync(code);
        Product = product;
        Query = product.Code;
        ProductImageUrl = _apiClient.ToAbsoluteUrl(product.ImagePath);
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));

        Colors.Clear();
        foreach (var c in product.Colors)
            Colors.Add(new LookupColorOption { Dto = c, FullImageUrl = _apiClient.ToAbsoluteUrl(c.ImagePath) });

        var preColor = preselectBarcode is null ? null
            : Colors.FirstOrDefault(c => c.Dto.Sizes.Any(s => s.Barcode == preselectBarcode));
        SelectColor(preColor ?? (Colors.Count == 1 ? Colors[0] : null));
        if (preselectBarcode is not null)
            SelectSize(Sizes.FirstOrDefault(s => s.Dto.Barcode == preselectBarcode));
    }

    [RelayCommand]
    private void SelectColor(LookupColorOption? color)
    {
        foreach (var c in Colors) c.IsSelected = c == color;
        SelectedColor = color;
        SelectedSize = null;
        Sizes.Clear();
        if (color is null) return;
        foreach (var s in color.Dto.Sizes)
            Sizes.Add(new LookupSizeOption { Dto = s });
        if (color.FullImageUrl is not null) ProductImageUrl = color.FullImageUrl;
        if (Sizes.Count == 1) SelectSize(Sizes[0]);
    }

    [RelayCommand]
    private void SelectSize(LookupSizeOption? size)
    {
        foreach (var s in Sizes) s.IsSelected = s == size;
        SelectedSize = size;
        if (size is not null && !size.IsSellable)
            SetError($"{size.Size} bedeni satisa kapali ya da fiyati tanimsiz.");
        else
            ClearError();
    }

    [RelayCommand]
    private void AddToCart()
    {
        if (SelectedColor is null) { SetError("Once renk secin."); return; }
        if (SelectedSize is null) { SetError("Once beden secin."); return; }
        if (!SelectedSize.IsSellable) { SetError("Bu beden satisa kapali."); return; }
        AddToCartRequested?.Invoke(SelectedSize.Dto.Barcode);
        IsOpen = false;
    }
}
