using LCWaikiki.POS.WpfClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Views;

public partial class MainPosWindow : Window
{
    private readonly MainPosViewModel _viewModel;
    private readonly DispatcherTimer _clock;

    public MainPosWindow(MainPosViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // ── Saat ────────────────────────────────────────────
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) =>
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

        // ── Numpad click handler (sepet + odeme, "pay-" prefix ile ayrilir) ──
        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnNumpadButtonClick));

        // ── Iade-Degisim: ayni pencereye geri donmek uzere ayri pencereyi ac ──
        viewModel.OpenExchangeRequested += () => OpenExchangeWindow(null);

        // "Onceki Belge" panelinden: iade penceresi o fis yuklu olarak acilir
        viewModel.OpenExchangeWithReceiptRequested += receipt => OpenExchangeWindow(receipt);

        // Esc: en ustteki arac panelini / popup'i kapatir
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (_viewModel.IsManagerApprovalOpen) { _viewModel.CancelManagerApprovalCommand.Execute(null); e.Handled = true; return; }
            if (_viewModel.IsDiscountPanelOpen) { _viewModel.CancelDiscountCommand.Execute(null); e.Handled = true; return; }
            if (_viewModel.IsManagerDiscountPanelOpen) { _viewModel.CancelManagerDiscountCommand.Execute(null); e.Handled = true; return; }
            if (_viewModel.IsCustomerSearchOpen) { _viewModel.IsCustomerSearchOpen = false; e.Handled = true; return; }
            if (_viewModel.CloseTopmostToolPanel()) e.Handled = true;
        };

        // ── Odeme overlay acilinca tutar kutusuna fokus ver ──
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainPosViewModel.IsPaymentOpen))
            {
                if (viewModel.IsPaymentOpen)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        PaymentAmountBox.Focus();
                        Keyboard.Focus(PaymentAmountBox);
                    }, DispatcherPriority.Input);
                }
                else
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        BarcodeBox.Focus();
                        Keyboard.Focus(BarcodeBox);
                    }, DispatcherPriority.Input);
                }
            }
            else if (e.PropertyName == nameof(MainPosViewModel.IsManagerApprovalOpen))
            {
                // PasswordBox binding desteklemez - her acilis/kapanista temizle
                ApprovalPinBox.Password = string.Empty;
                if (viewModel.IsManagerApprovalOpen)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        ApprovalPersonelIdBox.Focus();
                        Keyboard.Focus(ApprovalPersonelIdBox);
                    }, DispatcherPriority.Input);
                }
            }
            else if (e.PropertyName == nameof(MainPosViewModel.IsSubDiscountPanelOpen)
                     && viewModel.IsSubDiscountPanelOpen)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SubDiscountInputBox.Focus();
                    Keyboard.Focus(SubDiscountInputBox);
                }, DispatcherPriority.Input);
            }
        };

        // ── Pencere acildiginda barkod kutusuna fokus ────────
        ContentRendered += (_, _) =>
        {
            Activate();
            BarcodeBox.Focus();
            Keyboard.Focus(BarcodeBox);
        };
    }

    /// <summary>
    /// Iade-Degisim penceresini acar (bu pencere gizlenir, "Satisa Gec"/"Geri Don"
    /// ile geri donulur). receiptNumber verilirse o fis Fisle Iade popup'inda yuklenir.
    /// </summary>
    private void OpenExchangeWindow(string? receiptNumber)
    {
        var exchangeWindow = App.Services.GetRequiredService<IadeDegisimWindow>();
        exchangeWindow.TargetPosWindow = this;

        if (exchangeWindow.DataContext is ExchangeViewModel exchangeVm)
        {
            // Fis sepetinde zaten bekleyen iadeler: ayni fisten kalan adet asilmasin
            exchangeVm.SetPendingReturnsInPosCart(_viewModel.GetPendingReturnQuantities());
            if (!string.IsNullOrEmpty(receiptNumber))
                exchangeWindow.ContentRendered += async (_, _) => await exchangeVm.PreloadReceiptAsync(receiptNumber);
        }

        Hide();
        exchangeWindow.Show();
    }

    private void OnNumpadButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button btn) return;
        if (btn.Focusable) return;

        var tag = btn.Tag as string;
        if (string.IsNullOrEmpty(tag)) return;

        // Odeme numpad'i "pay-" prefix ile calisir, PaymentVM'e yazar
        if (tag.StartsWith("pay-"))
        {
            HandlePaymentNumpad(tag[4..]);
            e.Handled = true;
            return;
        }

        // Ara Toplam numpad'i "sub-" prefix ile calisir, SubDiscountInput'a yazar
        if (tag.StartsWith("sub-"))
        {
            HandleSubDiscountNumpad(tag[4..]);
            e.Handled = true;
            return;
        }

        // Satis danismani numpad'i "sp-" prefix ile calisir, SalesPersonInput'a yazar
        if (tag.StartsWith("sp-"))
        {
            var key = tag[3..];
            _viewModel.SalesPersonInput = key switch
            {
                "backspace" => _viewModel.SalesPersonInput.Length > 0 ? _viewModel.SalesPersonInput[..^1] : string.Empty,
                "clear" => string.Empty,
                _ => _viewModel.SalesPersonInput.Length < 10 ? _viewModel.SalesPersonInput + key : _viewModel.SalesPersonInput
            };
            e.Handled = true;
            return;
        }

        // Musteri Arama numpad'i "cust-" prefix ile calisir, PhoneInput'a yazar
        if (tag.StartsWith("cust-"))
        {
            HandleCustomerSearchNumpad(tag[5..]);
            e.Handled = true;
            return;
        }

        switch (tag)
        {
            case "backspace":
                if (_viewModel.BarcodeInput.Length > 0)
                    _viewModel.BarcodeInput = _viewModel.BarcodeInput[..^1];
                break;
            case "clear":
                _viewModel.BarcodeInput = string.Empty;
                break;
            case "enter":
                _ = _viewModel.ScanBarcodeAsync();
                break;
            case "clearcart":
                _viewModel.ClearCartCommand.Execute(null);
                break;
            default:
                _viewModel.BarcodeInput += tag;
                break;
        }

        BarcodeBox.CaretIndex = BarcodeBox.Text.Length;
        BarcodeBox.Focus();
        Keyboard.Focus(BarcodeBox);
        e.Handled = true;
    }

    private void HandlePaymentNumpad(string action)
    {
        var vm = _viewModel.PaymentVM;

        switch (action)
        {
            case "backspace":
                if (vm.AmountInput.Length > 0)
                    vm.AmountInput = vm.AmountInput[..^1];
                break;
            case "clear":
                vm.AmountInput = string.Empty;
                break;
            case "comma":
                if (!vm.AmountInput.Contains(','))
                    vm.AmountInput += vm.AmountInput.Length == 0 ? "0," : ",";
                break;
            case "clearall":
                vm.PaymentLines.Clear();
                break;
            case "enter":
                // Ayri "Satisi Tamamla" butonu artik yok - checkmark bu rolu ustleniyor
                if (vm.CompleteSaleCommand.CanExecute(null))
                    vm.CompleteSaleCommand.Execute(null);
                return;
            default:
                vm.AmountInput += action;
                break;
        }

        PaymentAmountBox.CaretIndex = PaymentAmountBox.Text.Length;
        PaymentAmountBox.Focus();
        Keyboard.Focus(PaymentAmountBox);
    }

    private void HandleSubDiscountNumpad(string action)
    {
        switch (action)
        {
            case "backspace":
                if (_viewModel.SubDiscountInput.Length > 0)
                    _viewModel.SubDiscountInput = _viewModel.SubDiscountInput[..^1];
                break;
            case "clear":
            case "clearall":
                _viewModel.SubDiscountInput = string.Empty;
                break;
            case "comma":
                if (!_viewModel.SubDiscountInput.Contains(','))
                    _viewModel.SubDiscountInput += _viewModel.SubDiscountInput.Length == 0 ? "0," : ",";
                break;
            case "enter":
                if (_viewModel.ApplySubDiscountCommand.CanExecute(null))
                    _viewModel.ApplySubDiscountCommand.Execute(null);
                return;
            default:
                _viewModel.SubDiscountInput += action;
                break;
        }

        if (_viewModel.IsSubDiscountPanelOpen)
        {
            SubDiscountInputBox.CaretIndex = SubDiscountInputBox.Text.Length;
            SubDiscountInputBox.Focus();
            Keyboard.Focus(SubDiscountInputBox);
        }
    }

    private void HandleCustomerSearchNumpad(string action)
    {
        var vm = _viewModel.CustomerSearchVM;

        switch (action)
        {
            case "backspace":
                if (vm.PhoneInput.Length > 0)
                    vm.PhoneInput = vm.PhoneInput[..^1];
                break;
            case "clear":
            case "clearall":
                vm.PhoneInput = string.Empty;
                break;
            case "comma":
                // Telefon numarasinda virgul anlamli degil, yoksayilir
                break;
            case "enter":
                if (vm.SearchCommand.CanExecute(null))
                    vm.SearchCommand.Execute(null);
                return;
            default:
                vm.PhoneInput += action;
                break;
        }

        CustomerPhoneBox.CaretIndex = CustomerPhoneBox.Text.Length;
        CustomerPhoneBox.Focus();
        Keyboard.Focus(CustomerPhoneBox);
    }

    /// <summary>
    /// Dokunmatik ekranda fiziksel klavye olmadigi icin, bir TextBox'a
    /// odaklanildiginda Windows'un dokunmatik ekran klavyesini (TabTip.exe)
    /// tetikler. Klavye zaten aciksa ya da baslatilamazsa sessizce yoksayilir
    /// (fiziksel klavyeli test/geliştirme ortaminda hata vermesin diye).
    /// </summary>
    private void TextBox_GotFocus_OpenKeyboard(object sender, RoutedEventArgs e)
    {
        try
        {
            var tabTipPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "microsoft shared", "ink", "TabTip.exe");

            if (System.IO.File.Exists(tabTipPath))
                System.Diagnostics.Process.Start(tabTipPath);
        }
        catch
        {
            // Klavye baslatilamadi (orn. gelistirme makinesinde) - sessizce gec
        }
    }

    private void ApprovalPinBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ApprovalPin = ApprovalPinBox.Password;

    private void ApprovalPinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (_viewModel.ConfirmManagerApprovalCommand.CanExecute(null))
            _viewModel.ConfirmManagerApprovalCommand.Execute(null);
    }

    /// <summary>Musteri arama: fiziksel klavye / okuyucu Enter'i aramayi baslatir.</summary>
    private void CustomerPhoneBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var cmd = _viewModel.CustomerSearchVM.SearchCommand;
        if (cmd.CanExecute(null)) cmd.Execute(null);
    }

        private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = _viewModel.ScanBarcodeAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();
        base.OnClosed(e);
    }
}