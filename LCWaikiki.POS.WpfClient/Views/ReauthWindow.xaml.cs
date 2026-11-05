using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LCWaikiki.POS.WpfClient.Views;

/// <summary>
/// Oturum (JWT) suresi doldugunda acilir. Ayni personel PIN'ini girer;
/// basariliysa bekleyen istek yeni token'la otomatik tekrar gonderilir.
/// Baska kasiyer bu pencereden giris yapamaz (personel no sabit).
/// </summary>
public partial class ReauthWindow : Window
{
    private readonly AuthService _auth;
    private readonly string _personelId;
    private int _failures;

    public ReauthWindow(AuthService auth, string personelId, string? fullName)
    {
        InitializeComponent();
        _auth = auth;
        _personelId = personelId;
        UserText.Text = $"{fullName ?? "Kasiyer"}  ·  Personel No {personelId}";
        ContentRendered += (_, _) => { PinBox.Focus(); Keyboard.Focus(PinBox); };
    }

    /// <summary>UI thread'de modal olarak gosterir; basariliysa true.</summary>
    public static Task<bool> ShowForAsync(AuthService auth)
    {
        var user = auth.CurrentUser;
        if (user is null) return Task.FromResult(false);
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.InvokeAsync(() =>
        {
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                        ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
            var dlg = new ReauthWindow(auth, user.PersonelId, user.FullName);
            if (owner is not null && owner != dlg) dlg.Owner = owner;
            return dlg.ShowDialog() == true;
        }).Task;
    }

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (PinBox.Password.Length < 6) PinBox.Password += (string)((Button)sender).Content;
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        if (PinBox.Password.Length > 0) PinBox.Password = PinBox.Password[..^1];
    }

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; Ok_Click(sender, e); }
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        var pin = PinBox.Password;
        if (pin.Length < 4) { ShowError("PIN en az 4 haneli olmalidir."); return; }
        try
        {
            OkButton.IsEnabled = false;
            await _auth.LoginAsync(_personelId, pin);
            DialogResult = true;
        }
        catch (ApiException ex) when (ex.StatusCode is 401 or 400)
        {
            _failures++;
            PinBox.Clear();
            ShowError(_failures >= 3
                ? "PIN hatali. Cok fazla deneme: bir dakika bekleyin ya da oturumu kapatin."
                : "PIN hatali.");
            AppLog.Warn($"Yeniden giris: hatali PIN ({_failures}).");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            OkButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
