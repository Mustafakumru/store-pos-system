using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.Core.Services;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class CustomerSearchViewModel : BaseViewModel
{
    private readonly CustomerService _customerService;

    [ObservableProperty] private string _phoneInput = string.Empty;
    [ObservableProperty] private CustomerDto? _foundCustomer;
    [ObservableProperty] private bool _hasSearched;
    [ObservableProperty] private bool _showNewCustomerForm;

    // XAML'da sekme icerigi gorunurlugu icin (arama tarafi ShowNewCustomerForm'un tersini ister)
    public bool IsSearchTabActive => !ShowNewCustomerForm;
    partial void OnShowNewCustomerFormChanged(bool value)
        => OnPropertyChanged(nameof(IsSearchTabActive));

    /// <summary>Kasiyer "Musteri Ara" sekmesine elle tikladiginda.</summary>
    [RelayCommand]
    private void ShowSearchTab() => ShowNewCustomerForm = false;

    /// <summary>Kasiyer "Yeni Musteri" sekmesine elle tikladiginda.</summary>
    [RelayCommand]
    private void ShowNewCustomerTab() => ShowNewCustomerForm = true;

    // XAML'da BoolToVisibilityConverter ile kullanmak icin (FoundCustomer nullable object,
    // dogrudan bool converter'a baglanamaz)
    public bool HasFoundCustomer => FoundCustomer is not null;

    partial void OnFoundCustomerChanged(CustomerDto? value)
        => OnPropertyChanged(nameof(HasFoundCustomer));

    [ObservableProperty] private string _newFullName = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private bool _newSmsConsent = true;

    /// <summary>Kullanici bir musteriyi (mevcut ya da yeni olusturulan) onayladiginda tetiklenir.</summary>
    public event Action<CustomerDto>? CustomerSelected;

    /// <summary>Kullanici modali iptal ettiginde tetiklenir.</summary>
    public event Action? CancelRequested;

    public CustomerSearchViewModel(CustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>Modal her acildiginda cagrilir - onceki arama/form durumunu temizler.</summary>
    public void Reset()
    {
        PhoneInput = string.Empty;
        FoundCustomer = null;
        HasSearched = false;
        ShowNewCustomerForm = false;
        NewFullName = string.Empty;
        NewEmail = string.Empty;
        NewSmsConsent = true;
        ClearError();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneInput))
        {
            SetError("Telefon numarasi girin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            FoundCustomer = await _customerService.SearchByPhoneAsync(PhoneInput.Trim());
            HasSearched = true;
            ShowNewCustomerForm = FoundCustomer is null;
        }
        catch (Exception ex)
        {
            SetError($"Arama hatasi: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFullName))
        {
            SetError("Ad Soyad girin.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var request = new CreateCustomerRequest(
                PhoneInput.Trim(),
                NewFullName.Trim(),
                string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail.Trim(),
                NewSmsConsent);

            FoundCustomer = await _customerService.CreateAsync(request);
            ShowNewCustomerForm = false;
        }
        catch (Exception ex)
        {
            SetError($"Kayit hatasi: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        if (FoundCustomer is null)
        {
            SetError("Once musteri secin veya kaydedin.");
            return;
        }
        CustomerSelected?.Invoke(FoundCustomer);
    }

    [RelayCommand]
    private void Cancel()
        => CancelRequested?.Invoke();
}