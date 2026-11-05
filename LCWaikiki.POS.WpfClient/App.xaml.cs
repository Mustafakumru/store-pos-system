using LCWaikiki.POS.WpfClient.Core;
using LCWaikiki.POS.WpfClient.Core.Http;
using LCWaikiki.POS.WpfClient.Core.Services;
using LCWaikiki.POS.WpfClient.ViewModels;
using LCWaikiki.POS.WpfClient.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace LCWaikiki.POS.WpfClient;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // XAML binding'leri (StringFormat={}{0:N2} vb.) varsayilan olarak en-US
        // kulturuyle bicimleniyordu ("1,010.50"); converter kullanan alanlar ise
        // tr-TR ("1.010,50") - ayni ekranda iki farkli bicim gorunuyordu.
        var tr = System.Windows.Markup.XmlLanguage.GetLanguage("tr-TR");
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(tr));
        // Run/Span gibi metin parcalari FrameworkElement degil - ayrica gerekli
        FrameworkContentElement.LanguageProperty.OverrideMetadata(
            typeof(System.Windows.Documents.TextElement), new FrameworkPropertyMetadata(tr));

        // ── Log + global hata yakalama: beklenmeyen bir hata uygulamayi
        // kapatip sepeti kaybettirmesin; her hata log dosyasina yazilsin.
        AppLog.Initialize();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Islenmeyen hata (uygulama kapaniyor)", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Gozlemlenmeyen gorev hatasi", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Oturum suresi dolunca: PIN ile yeniden giris, bekleyen istek tekrarlanir
        var api = Services.GetRequiredService<ApiClient>();
        var auth = Services.GetRequiredService<AuthService>();
        api.ReauthenticateAsync = () => ReauthWindow.ShowForAsync(auth);

        // Baglanti gostergesi (ekranlardaki nokta) - periyodik saglik yoklamasi
        var settings = Services.GetRequiredService<ClientSettings>();
        ConnectivityMonitor.Instance.Start(api, TimeSpan.FromSeconds(Math.Max(5, settings.Api.HealthCheckSeconds)));

        // Girişte: diskteki kataloğu yükle, gönderim döngüsünü başlat, hemen bir tur çalıştır
        var catalog = Services.GetRequiredService<Core.Offline.OfflineCatalog>();
        var offlineSync = Services.GetRequiredService<Core.Offline.OfflineSyncService>();
        auth.LoggedIn += user =>
        {
            catalog.Load(user.StoreId);
            offlineSync.Start();
            _ = Task.Run(() => offlineSync.TickAsync());
        };

        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    private bool _showingCrashDialog;

    /// <summary>
    /// UI thread'deki beklenmeyen hata: loglanir, kasiyere kisa mesaj gosterilir ve
    /// uygulama ACIK kalir (eskiden uygulama kapaniyor, sepet kayboluyordu).
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Beklenmeyen hata (yakalandi, uygulama devam ediyor)", e.Exception);
        e.Handled = true;
        if (_showingCrashDialog) return;
        _showingCrashDialog = true;
        try
        {
            MessageBox.Show(
                "Beklenmeyen bir hata olustu; son islem tamamlanmamis olabilir." + Environment.NewLine +
                "Ekran acik kalacak, lutfen islemi kontrol edip tekrar deneyin." + Environment.NewLine + Environment.NewLine +
                "Ayrinti log dosyasina yazildi:" + Environment.NewLine + AppLog.CurrentFile,
                "WPOS - Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _showingCrashDialog = false; }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Ayarlar (appsettings.json) + API istemcisi ───────────
        var settings = ClientSettings.Load();
        services.AddSingleton(settings);
        services.AddSingleton<ApiClient>(_ => new ApiClient(
            settings.Api.BaseUrl,
            settings.Api.AllowUntrustedLocalhostCertificate,
            settings.Api.TimeoutSeconds));

        // ── Services ─────────────────────────────────────────────
        services.AddSingleton<AuthService>();
        services.AddTransient<PosService>();
        services.AddTransient<ProductService>();
        services.AddTransient<CustomerService>();
        services.AddTransient<GiftCardService>();
        services.AddTransient<CashSessionService>();
        services.AddTransient<ReportService>();
        services.AddTransient<ReceiptService>();
        services.AddTransient<SuspendedCartService>();
        services.AddTransient<CashDrawerService>();

        // ── Cevrimdisi satis: yerel katalog + disk kuyrugu + arka plan gonderimi ──
        var offlineQueue = new Core.Offline.OfflineSaleQueue(Core.Offline.OfflineSaleQueue.DefaultRoot, settings.Offline.TerminalNo);
        Core.Offline.OfflineSaleQueue.Current = offlineQueue;   // XAML sayaci
        services.AddSingleton(offlineQueue);
        services.AddSingleton(new Core.Offline.OfflineCatalog(
            System.IO.Path.Combine(Core.Offline.OfflineSaleQueue.DefaultRoot, "catalog")));
        services.AddSingleton<Core.Offline.OfflineSyncService>();
        services.AddTransient<OfflineSalesViewModel>();

        // ── ViewModels ───────────────────────────────────────────
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ExchangeViewModel>();
        services.AddTransient<IadeDegisimWindow>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<MainPosViewModel>();
        services.AddTransient<PaymentViewModel>();
        services.AddTransient<CustomerSearchViewModel>();
        services.AddTransient<ProductLookupViewModel>();
        services.AddTransient<ManagerApprovalViewModel>();
        services.AddTransient<CashSessionViewModel>();
        services.AddTransient<ReportsViewModel>();

        // ── Views ────────────────────────────────────────────────
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainMenuWindow>();
        services.AddTransient<MainPosWindow>();
        services.AddTransient<PaymentWindow>();
        
    }
}