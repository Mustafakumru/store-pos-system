using LCWaikiki.POS.WpfClient.Core.Models.Pos;
using LCWaikiki.POS.WpfClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Views;

public partial class PaymentWindow : Window
{
    private readonly PaymentViewModel _viewModel;
    private readonly DispatcherTimer _clock;

    public PaymentWindow(PaymentViewModel viewModel)
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

        // ── Numpad click handler ─────────────────────────────
        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnNumpadButtonClick));

        // ── Satis tamamlandi ─────────────────────────────────
        viewModel.SaleCompleted += OnSaleCompleted;

        // ── Geri don ────────────────────────────────────────
        viewModel.GoBackRequested += () =>
        {
            var mainPosVm = App.Services.GetRequiredService<MainPosViewModel>();
            var mainPosWindow = new MainPosWindow(mainPosVm);
            mainPosWindow.Show();
            Close();
        };

        // ── Pencere acildiginda tutar kutusuna fokus ─────────
        ContentRendered += (_, _) =>
        {
            Activate();
            AmountBox.Focus();
            Keyboard.Focus(AmountBox);
        };
    }

    private void OnNumpadButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button btn) return;
        if (btn.Focusable) return;

        var tag = btn.Tag as string;
        if (string.IsNullOrEmpty(tag)) return;

        switch (tag)
        {
            case "backspace":
                if (_viewModel.AmountInput.Length > 0)
                    _viewModel.AmountInput = _viewModel.AmountInput[..^1];
                break;
            case "clear":
                _viewModel.AmountInput = string.Empty;
                break;
            case "tumu":
                _viewModel.AmountInput = _viewModel.RemainingAmount
                    .ToString("F2")
                    .Replace(".", ",");
                break;
            default:
                _viewModel.AmountInput += tag;
                break;
        }

        AmountBox.CaretIndex = AmountBox.Text.Length;
        AmountBox.Focus();
        Keyboard.Focus(AmountBox);
        e.Handled = true;
    }

    private void OnSaleCompleted(ProcessSaleResponse response)
    {
        MessageBox.Show(
            $"Satis tamamlandi!\nFis No: {response.ReceiptNumber}\nToplam: {response.FinalAmount:F2} TL",
            "Basarili",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        var mainPosVm = App.Services.GetRequiredService<MainPosViewModel>();
        mainPosVm.ClearCartCommand.Execute(null);

        var mainPosWindow = new MainPosWindow(mainPosVm);
        mainPosWindow.Show();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();
        base.OnClosed(e);
    }
}