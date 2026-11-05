using LCWaikiki.POS.WpfClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace LCWaikiki.POS.WpfClient.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginSucceeded += () =>
        {
            var menuVm = App.Services.GetRequiredService<MainMenuViewModel>();

            // Oturum boyunca TEK bir Fis ekrani: menuden her "Satis"
            // tiklamasinda yeni pencere (ve yeni bos sepet) acilmaz, mevcut
            // pencere (gizli olsa bile - orn. Iade-Degisim acikken) one getirilir.
            MainPosWindow? currentPosWindow = null;
            MainMenuWindow? menuWindow = null;
            MainPosWindow GetPosWindow()
            {
                if (currentPosWindow is null)
                {
                    currentPosWindow = App.Services.GetRequiredService<MainPosWindow>();
                    currentPosWindow.Closed += (_, _) => currentPosWindow = null;

                    // Fis/Iade ekranindan "Hediye Karti Ekle / Coklu H.Kart Yukleme":
                    // hediye karti satisi ayri bir belgedir (emanet) - Ana Menu'deki
                    // H.Kart Satis paneli one getirilir; Fis sepeti korunur.
                    if (currentPosWindow.DataContext is MainPosViewModel posVm)
                    {
                        posVm.OpenGiftCardSaleRequested += () =>
                        {
                            if (menuWindow is null) return;
                            menuWindow.Show();
                            menuWindow.WindowState = WindowState.Maximized;
                            menuWindow.Activate();
                            menuVm.OpenGiftCardSaleCommand.Execute(null);
                        };
                    }
                }
                return currentPosWindow;
            }

            menuVm.OpenPosRequested += () =>
            {
                var posWindow = GetPosWindow();
                posWindow.Show();
                posWindow.Activate();
            };

            // Ana Menu "Fiyat Gor / Urun Bul" panelinden "Sepete Ekle"
            menuVm.AddToPosRequested += async barcode =>
            {
                var posWindow = GetPosWindow();
                posWindow.Show();
                posWindow.Activate();
                if (posWindow.DataContext is MainPosViewModel posVm)
                {
                    await posVm.AddProductByBarcodeAsync(barcode);
                    if (string.IsNullOrEmpty(posVm.ErrorMessage))
                        posVm.SuccessMessage = $"Sepete eklendi ({barcode})";
                }
            };

            // Ana Menu kisayollari: Fatura / Tax Free / Istisnai Satis
            menuVm.OpenPosWithModeRequested += mode =>
            {
                var posWindow = GetPosWindow();
                posWindow.Show();
                posWindow.Activate();
                if (posWindow.DataContext is not MainPosViewModel posVm) return;
                switch (mode)
                {
                    case PosOpenMode.Invoice:
                        posVm.ContinueWithInvoiceCommand.Execute(null);
                        break;
                    case PosOpenMode.TaxFree:
                        posVm.OpenCountryPanelCommand.Execute(null);
                        break;
                    case PosOpenMode.Exceptional:
                        posVm.ShowExceptionalSaleHint();
                        break;
                }
            };

            menuVm.OpenExchangeRequested += () =>
            {
                var posWindow = GetPosWindow();
                // MainPosWindow kendi constructor'inda DataContext = viewModel
                // atiyor - o yuzden burada MainPosViewModel'i ayrica cozmeye
                // gerek yok, doğrudan DataContext'ten alip Degisim ekranini
                // aciyoruz.
                // OpenExchange, Fis penceresini gizleyip Iade-Degisim'i acar;
                // "Satisa Gec"/"Iptal" ile ayni Fis penceresine geri donulur.
                // (Burada ayrica posWindow.Show() CAGRILMAZ - eskiden iki
                // pencere ust uste aciliyordu.)
                if (posWindow.DataContext is MainPosViewModel posVm)
                    posVm.OpenExchangeCommand.Execute(null);
            };

            menuVm.LogoutRequested += () =>
            {
                var loginWindow = App.Services.GetRequiredService<LoginWindow>();
                loginWindow.Show();

                // Ana Menu, Fis ekrani (gizli olsa bile) ve Iade-Degisim dahil
                // oturuma ait tum pencereleri kapat - aksi halde cikis yapan
                // kasiyerin ekranlari arkada acik kaliyordu.
                foreach (var window in Application.Current.Windows.Cast<Window>().ToList())
                {
                    if (!ReferenceEquals(window, loginWindow))
                        window.Close();
                }
            };

            menuWindow = new MainMenuWindow(menuVm);
            menuWindow.Show();
            Close();
        };

        ContentRendered += (_, _) =>
        {
            Activate();
            Keyboard.Focus(PersonelIdBox);
        };
    }

    private void PersonelIdBox_GotFocus(object sender, RoutedEventArgs e)
        => _viewModel.IsPersonelIdActive = true;

    private void PinArea_Click(object sender, MouseButtonEventArgs e)
        => _viewModel.IsPersonelIdActive = false;
}