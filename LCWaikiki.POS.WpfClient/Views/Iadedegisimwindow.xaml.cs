using LCWaikiki.POS.WpfClient.ViewModels;
using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Views;

public partial class IadeDegisimWindow : Window
{
    private readonly ExchangeViewModel _viewModel;
    private readonly DispatcherTimer _clock;

    /// <summary>
    /// Bu pencere zaten acik bir MainPosWindow'dan (hamburger/ust sekme)
    /// acildiysa buraya atanir - "Satisa Gec"/"Iptal"de YENI bir Fis ekrani
    /// olusturmak yerine bu ayni pencereye geri donulur, mevcut sepeti 
    /// korunur. Ana Menu'den girildiyse null kalir, yeni pencere olusturulur.
    /// </summary>
    public MainPosWindow? TargetPosWindow
    {
        get => _targetPosWindow;
        set
        {
            _targetPosWindow = value;
            // Fis penceresi kapanirsa (cikis/uygulama kapanisi) artik ona donulmez
            if (value is not null)
                value.Closed += (_, _) => _targetPosWindow = null;
        }
    }
    private MainPosWindow? _targetPosWindow;

    // "Satisa Gec"/"Geri Don" ile kapatilirken true; pencere X ile
    // kapatilirsa false kalir ve gizli Fis ekrani geri gosterilir (aksi halde
    // Fis penceresi arka planda gizli kaliyordu).
    private bool _closingHandled;

    public IadeDegisimWindow(ExchangeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Reset();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnNumpadButtonClick));

        viewModel.TransferToSaleRequested += (returnItems, customer) =>
        {
            var posWindow = TargetPosWindow
                ?? App.Services.GetRequiredService<MainPosWindow>();

            if (posWindow.DataContext is MainPosViewModel posVm)
            {
                posVm.AttachPendingReturns(returnItems, customer);
                // Iade ekraninda "Belge Tipini Degistir" ile fatura istendiyse
                // alici bilgisi Fis ekraninda hemen sorulur.
                if (viewModel.RequestInvoice && !posVm.IsInvoice)
                    posVm.ContinueWithInvoiceCommand.Execute(null);
            }

            _closingHandled = true;
            posWindow.Show();
            posWindow.Activate();
            Close();
        };

        viewModel.CancelRequested += () =>
        {
            _closingHandled = true;
            TargetPosWindow?.Show();
            TargetPosWindow?.Activate();
            Close();
        };

        // Popup acilinca/kapaninca ilgili giris kutusuna fokus ver
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(ExchangeViewModel.IsManualPricePanelOpen)
                or nameof(ExchangeViewModel.IsReceiptSearchOpen)
                or nameof(ExchangeViewModel.IsPhoneSearchOpen)
                or nameof(ExchangeViewModel.IsCustomerSearchOpen)))
                return;

            Dispatcher.BeginInvoke(() =>
            {
                var target = FocusTargetForCurrentState();
                target.Focus();
                Keyboard.Focus(target);
                target.SelectAll();
            }, DispatcherPriority.Input);
        };

        PreviewKeyDown += OnWindowPreviewKeyDown;

        // Hediye karti satisi: Ana Menu'deki H.Kart Satis paneli (Fis ekrani uzerinden)
        viewModel.GiftCardSaleRequested += () =>
        {
            if (TargetPosWindow?.DataContext is MainPosViewModel posVm)
                posVm.RaiseGiftCardSaleRequested();
        };

        ContentRendered += (_, _) =>
        {
            Activate();
            BarcodeBox.Focus();
            Keyboard.Focus(BarcodeBox);
        };
    }

    private void OnNumpadButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button btn) return;
        if (btn.Focusable) return;

        var tag = btn.Tag as string;
        if (string.IsNullOrEmpty(tag)) return;

        // Musteri Arama numpad'i "cust-" prefix ile calisir, PhoneInput'a yazar
        // (eskiden bu prefix ele alinmiyordu: tuslar barkod kutusuna "cust-1"
        // yaziyordu ve telefonla musteri aramak hic mumkun degildi).
        if (tag.StartsWith("cust-"))
        {
            HandleCustomerSearchNumpad(tag[5..]);
            e.Handled = true;
            return;
        }

        // Hedef alan: hangi popup aciksa ona, aksi halde arama barkoduna.
        TextBox targetBox = _viewModel.IsManualPricePanelOpen ? ManualPriceBox
            : _viewModel.IsReceiptSearchOpen ? ReceiptSearchBox
            : _viewModel.IsPhoneSearchOpen ? PhoneSearchBarcodeBox
            : BarcodeBox;

        string GetValue() => _viewModel.IsManualPricePanelOpen ? _viewModel.ManualPriceInput
            : _viewModel.IsReceiptSearchOpen ? _viewModel.ReceiptNumberInput
            : _viewModel.SearchBarcodeInput;

        void SetValue(string v)
        {
            if (_viewModel.IsManualPricePanelOpen) _viewModel.ManualPriceInput = v;
            else if (_viewModel.IsReceiptSearchOpen) _viewModel.ReceiptNumberInput = v;
            else _viewModel.SearchBarcodeInput = v;
        }

        switch (tag)
        {
            case "backspace":
                var current = GetValue();
                if (current.Length > 0)
                    SetValue(current[..^1]);
                break;
            case "clear":
                SetValue(string.Empty);
                break;
            case "comma":
                var val = GetValue();
                if (!val.Contains(','))
                    SetValue(val.Length == 0 ? "0," : val + ",");
                break;
            case "enter":
                if (_viewModel.IsManualPricePanelOpen)
                {
                    if (_viewModel.ConfirmManualPriceCommand.CanExecute(null))
                        _viewModel.ConfirmManualPriceCommand.Execute(null);
                }
                else if (_viewModel.IsReceiptSearchOpen)
                {
                    if (_viewModel.LoadReceiptForPopupCommand.CanExecute(null))
                        _viewModel.LoadReceiptForPopupCommand.Execute(null);
                }
                else if (_viewModel.IsPhoneSearchOpen)
                {
                    if (_viewModel.SearchByCustomerCommand.CanExecute(null))
                        _viewModel.SearchByCustomerCommand.Execute(null);
                }
                else
                {
                    if (_viewModel.ScanBarcodeDirectCommand.CanExecute(null))
                        _viewModel.ScanBarcodeDirectCommand.Execute(null);
                }
                return;
            default:
                SetValue(GetValue() + tag);
                break;
        }

        targetBox.CaretIndex = targetBox.Text.Length;
        targetBox.Focus();
        Keyboard.Focus(targetBox);
        e.Handled = true;
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
    /// <summary>Fisle Iade popup'inda listedeki bir satira cift tiklaninca o urunu iade sepetine ekler.</summary>
    private void ReceiptItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { Content: SaleItemDto item }) return;
        if (_viewModel.AddReceiptItemToCartCommand.CanExecute(item))
            _viewModel.AddReceiptItemToCartCommand.Execute(item);
    }

    /// <summary>Musteri Bilgisiyle Iade popup'inda listedeki bir satira cift tiklaninca o urunu iade sepetine ekler.</summary>
    private void CustomerPurchaseItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { Content: CustomerPurchaseDto purchase }) return;
        if (_viewModel.AddCustomerPurchaseToReturnCommand.CanExecute(purchase))
            _viewModel.AddCustomerPurchaseToReturnCommand.Execute(purchase);
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
                if (vm.PhoneInput.Length < 11)
                    vm.PhoneInput += action;
                break;
        }

        CustomerPhoneBox.CaretIndex = CustomerPhoneBox.Text.Length;
        CustomerPhoneBox.Focus();
        Keyboard.Focus(CustomerPhoneBox);
    }

    private TextBox FocusTargetForCurrentState()
        => _viewModel.IsManualPricePanelOpen ? ManualPriceBox
            : _viewModel.IsCustomerSearchOpen ? CustomerPhoneBox
            : _viewModel.IsReceiptSearchOpen ? ReceiptSearchBox
            : _viewModel.IsPhoneSearchOpen ? PhoneSearchBarcodeBox
            : BarcodeBox;

    /// <summary>Esc: en ustteki popup'i kapatir; popup yoksa "Geri Don" ile ayni.</summary>
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (_viewModel.CloseTopmostPopup())
        {
            var target = FocusTargetForCurrentState();
            target.Focus();
            Keyboard.Focus(target);
        }
        else if (_viewModel.CancelCommand.CanExecute(null))
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }

    private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var cmd = _viewModel.ScanBarcodeDirectCommand;
            if (cmd.CanExecute(null)) cmd.Execute(null);
        }
    }

    // Fiziksel klavye / barkod okuyucu Enter'i: popup giris kutulari
    private void ReceiptSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (_viewModel.LoadReceiptForPopupCommand.CanExecute(null))
            _viewModel.LoadReceiptForPopupCommand.Execute(null);
    }

    private void PhoneSearchBarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (_viewModel.SearchByCustomerCommand.CanExecute(null))
            _viewModel.SearchByCustomerCommand.Execute(null);
    }

    private void ManualPriceBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (_viewModel.ConfirmManualPriceCommand.CanExecute(null))
            _viewModel.ConfirmManualPriceCommand.Execute(null);
    }

    private void CustomerPhoneBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var cmd = _viewModel.CustomerSearchVM.SearchCommand;
        if (cmd.CanExecute(null)) cmd.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();

        // Pencere X / Alt+F4 ile kapatildiysa gizli Fis ekranini geri getir
        if (!_closingHandled && TargetPosWindow is { } pos
            && !Dispatcher.HasShutdownStarted
            && Application.Current is { } app && app.Windows.OfType<Window>().Contains(pos))
        {
            pos.Show();
            pos.Activate();
        }
        base.OnClosed(e);
    }
}