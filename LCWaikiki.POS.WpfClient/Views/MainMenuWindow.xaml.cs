using LCWaikiki.POS.WpfClient.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Views;

public partial class MainMenuWindow : Window
{
	private readonly DispatcherTimer _clock;
	private readonly MainMenuViewModel _viewModel;

	public MainMenuWindow(MainMenuViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
		_viewModel = viewModel;

		_clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_clock.Tick += (_, _) =>
			ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
		_clock.Start();
		ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

		viewModel.ClearPinRequested += () => ManagerPinBox.Clear();
		viewModel.ClearChangePinRequested += () =>
		{
			OldPinBox.Clear();
			NewPinBox.Clear();
			NewPinRepeatBox.Clear();
		};

		// Kasa durumu: acilista ve menuye her donuste (Fis/Iade ekranindan) tazelenir
		Loaded += async (_, _) => await viewModel.RefreshSessionAsync();
		Activated += async (_, _) => await viewModel.RefreshSessionAsync();
	}

	/// <summary>PasswordBox binding desteklemedigi icin PIN code-behind'dan aktarilir.</summary>
	private void ManagerPinBox_PasswordChanged(object sender, RoutedEventArgs e)
		=> _viewModel.ManagerPin = ManagerPinBox.Password;

	private void ChangePinBox_PasswordChanged(object sender, RoutedEventArgs e)
	{
		_viewModel.OldPin = OldPinBox.Password;
		_viewModel.NewPin = NewPinBox.Password;
		_viewModel.NewPinRepeat = NewPinRepeatBox.Password;
	}

	protected override void OnClosed(EventArgs e)
	{
		_clock.Stop();
		base.OnClosed(e);
	}
}
