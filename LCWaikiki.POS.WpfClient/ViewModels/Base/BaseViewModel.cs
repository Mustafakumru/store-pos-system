using CommunityToolkit.Mvvm.ComponentModel;

namespace LCWaikiki.POS.WpfClient.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    protected void SetError(string message) => ErrorMessage = message;
    protected void ClearError() => ErrorMessage = string.Empty;
}