using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Services;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly AuthService _authService;

    [ObservableProperty] private string _personelId = string.Empty;
    [ObservableProperty] private string _pin = string.Empty;
    [ObservableProperty] private string _pinDisplay = string.Empty;

    // Numpad hangi alana yazacak: true = PersonelId, false = PIN
    [ObservableProperty] private bool _isPersonelIdActive = true;

    public event Action? LoginSucceeded;

    public LoginViewModel(AuthService authService)
        => _authService = authService;

    [RelayCommand]
    private void AppendDigit(string digit)
    {
        if (IsBusy) return;

        if (IsPersonelIdActive)
        {
            PersonelId += digit;
        }
        else
        {
            Pin += digit;
            PinDisplay = string.Join(" ", Enumerable.Repeat("●", Pin.Length));
        }
    }

    [RelayCommand]
    private void Backspace()
    {
        if (IsPersonelIdActive)
        {
            if (PersonelId.Length > 0)
                PersonelId = PersonelId[..^1];
        }
        else if (Pin.Length > 0)
        {
            Pin = Pin[..^1];
            PinDisplay = string.Join(" ", Enumerable.Repeat("●", Pin.Length));
        }
    }

    [RelayCommand]
    private void Clear()
    {
        if (IsPersonelIdActive)
        {
            PersonelId = string.Empty;
        }
        else
        {
            Pin = string.Empty;
            PinDisplay = string.Empty;
        }
    }

    // ══════════════════════ ILK GIRIS: PIN DEGISTIRME ══════════════════════
    // Ayni numpad kullanilir: 1) yeni PIN  2) yeni PIN tekrar. Onaylaninca
    // backend tam yetkili token doner ve normal akisa (Ana Menu) gecilir.

    [ObservableProperty] private bool _isChangePinMode;
    [ObservableProperty] private string _pinPrompt = "Sifre";
    [ObservableProperty] private string? _infoMessage;

    private string _loginPin = string.Empty;   // dogrulanan mevcut (varsayilan) PIN
    private string? _firstNewPin;              // 1. adimda girilen yeni PIN

    private void StartChangePin()
    {
        _loginPin = Pin;
        _firstNewPin = null;
        IsChangePinMode = true;
        IsPersonelIdActive = false;
        PinPrompt = "Yeni PIN (4-6 hane)";
        InfoMessage = "Ilk girisiniz (veya PIN'iniz sifirlandi). Devam etmek icin yeni bir PIN belirleyin.";
        Pin = string.Empty;
        PinDisplay = string.Empty;
    }

    private async Task ContinueChangePinAsync()
    {
        if (Pin.Length is < 4 or > 6)
        {
            SetError("PIN 4-6 haneli olmalidir.");
            return;
        }

        if (_firstNewPin is null)
        {
            // 1. adim tamam -> tekrar iste
            _firstNewPin = Pin;
            PinPrompt = "Yeni PIN (tekrar)";
            Pin = string.Empty;
            PinDisplay = string.Empty;
            return;
        }

        if (Pin != _firstNewPin)
        {
            SetError("PIN'ler eslesmiyor. Yeni PIN'i bastan girin.");
            _firstNewPin = null;
            PinPrompt = "Yeni PIN (4-6 hane)";
            Pin = string.Empty;
            PinDisplay = string.Empty;
            return;
        }

        try
        {
            IsBusy = true;
            await _authService.ChangePinAsync(_loginPin, Pin);
            IsChangePinMode = false;
            InfoMessage = null;
            _loginPin = string.Empty;
            LoginSucceeded?.Invoke();
        }
        catch (ApiException ex)
        {
            // Backend kurali (orn. 1111 / 1234 gibi tahmin edilebilir PIN) mesaji
            SetError(ex.Message);
            _firstNewPin = null;
            PinPrompt = "Yeni PIN (4-6 hane)";
            Pin = string.Empty;
            PinDisplay = string.Empty;
        }
        catch (Exception ex)
        {
            SetError($"Baglanti hatasi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelChangePin()
    {
        // PIN degistirilmeden cikis: oturum kapatilir, basa donulur
        _authService.Logout();
        IsChangePinMode = false;
        InfoMessage = null;
        PinPrompt = "Sifre";
        _loginPin = string.Empty;
        _firstNewPin = null;
        Pin = string.Empty;
        PinDisplay = string.Empty;
        IsPersonelIdActive = true;
        ClearError();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ClearError();

        if (IsChangePinMode)
        {
            await ContinueChangePinAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(PersonelId))
        {
            SetError("Kullanici kodu giriniz.");
            IsPersonelIdActive = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Pin))
        {
            SetError("Sifre giriniz.");
            IsPersonelIdActive = false;
            return;
        }

        try
        {
            IsBusy = true;
            var response = await _authService.LoginAsync(PersonelId, Pin);

            // Ilk giris / sifirlanmis PIN (varsayilan 1234): yeni PIN belirlenmeden
            // devam edilmez - backend de bu oturumla PIN degistirmek disinda
            // hicbir islem yaptirmaz.
            if (response.IsFirstLogin)
            {
                StartChangePin();
                return;
            }

            LoginSucceeded?.Invoke();
        }
        catch (ApiException ex)
        {
            // 401: hatali kod/PIN, 423: hesap kilitli, 429: cok fazla deneme
            SetError(ex.StatusCode == 401
                ? "Kullanici kodu veya sifre hatali."
                : ex.Message);
            Pin = string.Empty;
            PinDisplay = string.Empty;
        }
        catch (Exception ex)
        {
            SetError($"Baglanti hatasi: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}