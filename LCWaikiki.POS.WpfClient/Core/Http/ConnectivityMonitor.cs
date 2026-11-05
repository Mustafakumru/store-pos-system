using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;

namespace LCWaikiki.POS.WpfClient.Core.Http;

/// <summary>
/// Sunucu baglantisi gostergesi (ekranlardaki yesil/kirmizi nokta).
/// Her API cagrisinin sonucu ve 15 sn'de bir /api/health yoklamasi ile guncellenir.
/// XAML'dan: {Binding IsOnline, Source={x:Static http:ConnectivityMonitor.Instance}}
/// </summary>
public partial class ConnectivityMonitor : ObservableObject
{
    public static ConnectivityMonitor Instance { get; } = new();

    [ObservableProperty] private bool _isOnline = true;
    [ObservableProperty] private string _statusText = "Bagli";

    private DispatcherTimer? _timer;
    private ApiClient? _api;

    private ConnectivityMonitor() { }

    public void Start(ApiClient api, TimeSpan interval)
    {
        _api = api;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += async (_, _) => await PingAsync();
        _timer.Start();
        _ = PingAsync();
    }

    private async Task PingAsync()
    {
        if (_api is null) return;
        try { await _api.PingAsync(); }
        catch { /* sonucu ApiClient zaten bildirdi */ }
    }

    /// <summary>ApiClient her cagri sonucunu bildirir (UI disi thread'den de gelebilir).</summary>
    internal void Report(bool online)
    {
        void Apply()
        {
            if (IsOnline == online) return;
            IsOnline = online;
            StatusText = online ? "Bagli" : "Baglanti yok";
            if (online) AppLog.Info("Sunucu baglantisi geri geldi.");
            else AppLog.Warn("Sunucu baglantisi koptu.");
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Apply();
        else dispatcher.BeginInvoke(Apply);
    }
}
