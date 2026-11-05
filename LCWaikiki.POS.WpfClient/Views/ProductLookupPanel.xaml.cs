using LCWaikiki.POS.WpfClient.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Views;

/// <summary>Urun Bul / Fiyat Gor paneli (Fis ekrani ve Ana Menu'de ortak).</summary>
public partial class ProductLookupPanel : UserControl
{
    public ProductLookupPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged vm) vm.PropertyChanged += OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProductLookupViewModel.IsOpen)) return;
        if (sender is ProductLookupViewModel { IsOpen: true })
            Dispatcher.BeginInvoke(() =>
            {
                QueryBox.Focus();
                Keyboard.Focus(QueryBox);
                QueryBox.SelectAll();
            }, DispatcherPriority.Input);
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ProductLookupViewModel vm)
        {
            e.Handled = true;
            if (vm.SearchCommand.CanExecute(null)) vm.SearchCommand.Execute(null);
        }
        else if (e.Key == Key.Escape && DataContext is ProductLookupViewModel vm2)
        {
            e.Handled = true;
            vm2.CloseCommand.Execute(null);
        }
    }

    /// <summary>Dokunmatik kasada harf girisi icin Windows ekran klavyesi.</summary>
    private void QueryBox_GotFocus(object sender, RoutedEventArgs e)
    {
        try
        {
            var tabTip = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "microsoft shared", "ink", "TabTip.exe");
            if (System.IO.File.Exists(tabTip)) System.Diagnostics.Process.Start(tabTip);
        }
        catch { /* gelistirme makinesinde yoksay */ }
    }
}
